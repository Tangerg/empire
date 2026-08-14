import { Battlefield } from '../domain/battlefield';
import type { AiObjectiveAdvisorRegistry } from '../ai-objectives';
import { PriorityRegistry } from '../registry';
import type { Action, GameState, PlayerId, Unit } from '../types';
import type { ContentCatalog } from '../content-pack';
import type { AbilityAiEvaluatorRegistry } from './ability-evaluators';
import { readAgenda, type AiAgenda } from './agenda';
import type { AiOptions, AiRules } from './rules';
import { threatMap } from './threat';

export interface AiPlanningDependencies {
  readonly rules: AiRules;
  readonly objectiveAdvisors: AiObjectiveAdvisorRegistry;
  readonly abilityEvaluators: AbilityAiEvaluatorRegistry;
  readonly intents: AiIntentRegistry;
}

/**
 * One decision, and the analysis every decision is entitled to share.
 *
 * The agenda, the threat map and the battlefield projection are derived lazily:
 * a turn settled by a cheap intent never pays for the expensive ones, which is
 * exactly how the hand-written chain behaved before it became a registry.
 *
 * This is also the whole argument list of the planner's inner functions. They
 * used to take the pieces — state, side, agenda, threat, options, battlefield,
 * dependencies — which meant every caller disassembled the context and passed
 * it back one field at a time, and every new shared reading widened seven
 * signatures. Pass the context.
 */
export class AiTurnContext {
  private agendaCache: AiAgenda | null = null;
  private threatCache: Map<number, number> | null = null;
  private battlefieldCache: Battlefield | null = null;

  constructor(
    readonly planning: AiPlanningDependencies,
    readonly state: GameState,
    readonly player: PlayerId,
    readonly options: AiOptions,
  ) {}

  get rules(): AiRules {
    return this.planning.rules;
  }

  get content(): ContentCatalog {
    return this.rules.content;
  }

  get abilityEvaluators(): AbilityAiEvaluatorRegistry {
    return this.planning.abilityEvaluators;
  }

  /** What this side is playing for, read once from the board. */
  get agenda(): AiAgenda {
    return this.agendaCache ??= readAgenda(
      this.state,
      this.player,
      this.planning.objectiveAdvisors,
      this.rules.objectives,
      this.content,
    );
  }

  /** Threat weight per tile, including tiles a charging strike already marked. */
  get threat(): Map<number, number> {
    return this.threatCache ??= threatMap(this.rules, this.state, this.player, this.rules.space, this.content);
  }

  get battlefield(): Battlefield {
    return this.battlefieldCache ??= new Battlefield(this.state, this.content);
  }

  /**
   * Units the ordering policy entitles to act right now. Under per-unit orders
   * that is exactly one unit, and planning for any other would produce an
   * action execution is obliged to reject.
   */
  actors(): Unit[] {
    return this.rules.turnOrders.get(this.state.turnOrder.policy).actors(this.state)
      .filter((unit) => !unit.done);
  }
}

/**
 * One thing the AI considers doing.
 *
 * The driver used to be a fixed chain: a tactic, else a recruit, else a stance
 * change, else the best unit move. A rule plugin could already *add* an action
 * kind and have it executed — but nothing could make the AI ever choose one,
 * so half of every extension stopped at the human player.
 *
 * Priority keeps the original order rather than turning it into a score:
 * "a tactic pre-empts a march" is a decision about kinds of action, not a
 * comparison of their values, and inventing a common currency for it would
 * have been a rebalance disguised as a refactor.
 */
export interface AiIntent {
  readonly id: string;
  /** Lower goes first. The built-ins sit at 10/20/30/40, leaving room between. */
  readonly priority: number;
  propose(context: AiTurnContext): Action | null;
}

export class AiIntentRegistry extends PriorityRegistry<AiIntent> {
  constructor() {
    super('AI intent');
  }

  /** The order intents are consulted in, which is the order that decides turns. */
  ids(): string[] {
    return this.ordered().map((intent) => intent.id);
  }

  /** The first intent with something to propose. */
  choose(context: AiTurnContext): Action | null {
    for (const intent of this.ordered()) {
      const action = intent.propose(context);
      if (action) return action;
    }
    return null;
  }

  clone(): AiIntentRegistry {
    return this.copyInto(new AiIntentRegistry());
  }
}
