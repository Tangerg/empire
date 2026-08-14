import { applyAction, commandOptions } from './actions';
import { BattleLifecycle } from './turn-cycle';
import {
  chooseAction,
  type AbilityAiEvaluatorRegistry,
  type AiIntentRegistry,
  type AiOptions,
} from './ai';
import { type AiObjectiveAdvisorRegistry } from './ai-objectives';
import { ActionHandlerRegistry, type BattleRuleServices } from './action-system';
import {
  forecast,
  type CombatForecast,
} from './combat';
import {
  type CombatModifierPipeline,
} from './combat-modifiers';
import { forecastCombatPlan } from './combat-plan';
import { validateLevel } from './level-validation';
import { cloneState, createState, restoreState, type CreateStateOptions } from './state';
import { createBattleSave, type BattleSave } from './battle-save';
import type { ContentCatalog } from './content-pack';
import { careerOptions } from './careers';
import { activeTurnOrder, mayAct, type TurnOrderPolicy } from './turn-order';
import type { Action, Coord, GameEvent, GameState, LevelData, Unit, WeaponId } from './types';

export interface BattleEngineDependencies extends BattleRuleServices {
  readonly actionHandlers: ActionHandlerRegistry;
  readonly aiObjectiveAdvisors: AiObjectiveAdvisorRegistry;
  readonly abilityAiEvaluators: AbilityAiEvaluatorRegistry;
  readonly aiIntents: AiIntentRegistry;
}

export interface BattleDispatchReceipt {
  readonly events: GameEvent[];
  /** Snapshot immediately before the action, suitable for an undo stack. */
  readonly before: GameState;
}

export class BattleEngineConfigurationError extends Error {
  constructor(readonly issues: readonly string[]) {
    super(`invalid battle engine configuration:\n${issues.map((issue) => `- ${issue}`).join('\n')}`);
    this.name = 'BattleEngineConfigurationError';
  }
}

export class BattleLevelError extends Error {
  constructor(readonly levelId: string, readonly issues: readonly string[]) {
    super(`invalid battle level "${levelId}":\n${issues.map((issue) => `- ${issue}`).join('\n')}`);
    this.name = 'BattleLevelError';
  }
}

/**
 * Application-facing façade and dependency-injection boundary.
 *
 * It composes domain strategies but owns no UI, story, or singleton state. A
 * server simulation, mod sandbox, or alternate ruleset can create an isolated
 * instance with different handlers/providers.
 */
export class BattleEngine {
  readonly actionHandlers: ActionHandlerRegistry;
  readonly combatModifiers: CombatModifierPipeline;
  readonly aiObjectiveAdvisors: AiObjectiveAdvisorRegistry;
  readonly abilityAiEvaluators: AbilityAiEvaluatorRegistry;
  readonly aiIntents: AiIntentRegistry;
  readonly rules: BattleRuleServices;

  constructor(dependencies: BattleEngineDependencies) {
    this.actionHandlers = dependencies.actionHandlers;
    this.combatModifiers = dependencies.combatModifiers;
    this.aiObjectiveAdvisors = dependencies.aiObjectiveAdvisors;
    this.abilityAiEvaluators = dependencies.abilityAiEvaluators;
    this.aiIntents = dependencies.aiIntents;
    this.rules = {
      content: dependencies.content,
      abilities: dependencies.abilities,
      space: dependencies.space,
      combatModifiers: this.combatModifiers,
      hitEffects: dependencies.hitEffects,
      statusBehaviors: dependencies.statusBehaviors,
      scenarioConditions: dependencies.scenarioConditions,
      scenarioEffects: dependencies.scenarioEffects,
      objectives: dependencies.objectives,
      progression: dependencies.progression,
      areaShapes: dependencies.areaShapes,
      directives: dependencies.directives,
      referenceChecks: dependencies.referenceChecks,
      grids: dependencies.grids,
      saves: dependencies.saves,
      resources: dependencies.resources,
      turnOrders: dependencies.turnOrders,
      reactions: dependencies.reactions,
      unitDepartures: dependencies.unitDepartures,
      random: dependencies.random,
    };
    this.assertConfiguration();
  }

  /** The ruleset's content catalog. Presentation must read from this, never a global. */
  get content() {
    return this.rules.content;
  }

  createState(level: LevelData, options: CreateStateOptions = {}): GameState {
    const issues = validateLevel(this.rules, level)
      .filter((issue) => issue.severity === 'error')
      .map((issue) => issue.message);
    if (issues.length > 0) throw new BattleLevelError(level.id, issues);
    const state = createState(this.rules.content, level, options);
    // A level without a deployment phase is already playing, so it needs its
    // first actor turn now; deployment levels get theirs on finishDeployment.
    if (state.phase === 'playing') this.lifecycle(state).start();
    return state;
  }

  /** Phase and round transitions of one battle running under this ruleset. */
  lifecycle(state: GameState, emit?: (event: GameEvent) => void): BattleLifecycle {
    return new BattleLifecycle(state, this.rules, emit);
  }

  /** Turn-order policy this battle runs under. */
  turnOrder(state: GameState): TurnOrderPolicy {
    return activeTurnOrder(this.rules, state);
  }

  /** May this unit act right now, under the battle's ordering policy? */
  canAct(state: GameState, unit: Unit): boolean {
    return mayAct(this.rules, state, unit);
  }

  /** Units entitled to act in the current actor turn. */
  actors(state: GameState): Unit[] {
    return state.phase === 'playing' ? this.turnOrder(state).actors(state) : [];
  }

  /** Deterministic look-ahead for an initiative strip; empty for side turns. */
  turnOrderPreview(state: GameState, count = 8): Unit[] {
    const ids = this.turnOrder(state).preview(state, this.rules.content, count);
    return ids.flatMap((id) => {
      const unit = state.units.find((candidate) => candidate.id === id);
      return unit ? [unit] : [];
    });
  }

  cloneState(state: GameState): GameState {
    return cloneState(state);
  }

  /** Everything needed to resume this battle later, as a plain document. */
  saveBattle(state: GameState, savedAt?: string): BattleSave {
    return createBattleSave(state, savedAt);
  }

  /**
   * Rehydrates a battle onto this ruleset, or refuses it.
   *
   * The mirror of `createState`: a level is checked against the catalog and the
   * composition before play, and so is a save — against the same reference
   * checks, plus the content ids the battle has been played with since.
   */
  loadBattle(raw: unknown): GameState {
    return this.rules.saves.load(raw, this.rules).state;
  }

  dispatch(state: GameState, action: Action): GameEvent[] {
    return this.dispatchWithReceipt(state, action).events;
  }

  /**
   * Authoritative transactional command boundary.
   *
   * Handlers may use convenient in-place domain mutations; callers still get
   * all-or-nothing semantics, and stateful shells can reuse the same snapshot
   * for undo instead of cloning the aggregate a second time.
   */
  dispatchWithReceipt(state: GameState, action: Action): BattleDispatchReceipt {
    const before = cloneState(state);
    try {
      const events = applyAction(this, state, action);
      return { events, before };
    } catch (error) {
      restoreState(state, before);
      throw error;
    }
  }

  commandsAt(state: GameState, unit: Unit, at: Coord) {
    return commandOptions(this.rules, state, unit, at);
  }

  careerOptions(state: GameState, unit: Unit) {
    return careerOptions(this.rules, state, unit);
  }

  /**
   * Spatial questions are asked of `rules.space` directly.
   *
   * Seven methods here forwarded to it verbatim, binding neither the state nor
   * the ruleset — so a new spatial query meant editing the port, this façade and
   * the session shell above it. The port is public; a façade in front of a
   * public port is a third place to keep in step, not an abstraction.
   */
  get space() {
    return this.rules.space;
  }

  forecast(
    state: GameState,
    attacker: Unit,
    defender: Unit,
    attackFrom?: Coord,
    weapon?: WeaponId,
  ): CombatForecast {
    return forecast(this.rules, state, attacker, defender, { attackFrom, weapon });
  }

  attackPlan(state: GameState, attacker: Unit, aimedAt: Coord, attackFrom?: Coord, weapon?: WeaponId) {
    return forecastCombatPlan(this.rules, state, attacker, aimedAt, { from: attackFrom, weapon });
  }

  chooseAiAction(state: GameState, options?: Partial<AiOptions>): Action {
    return chooseAction({
      rules: this.rules,
      objectiveAdvisors: this.aiObjectiveAdvisors,
      abilityEvaluators: this.abilityAiEvaluators,
      intents: this.aiIntents,
    }, state, options);
  }

  private assertConfiguration(): void {
    const issues = this.rules.referenceChecks.contentIssues(this.rules);
    if (issues.length > 0) throw new BattleEngineConfigurationError(issues);
  }
}

/**
 * Focused rule overrides for one engine.
 *
 * `content` is required and is never defaulted to ambient state. Every other
 * capability, if given, replaces the default the rule plugins install — see
 * `createBattleEngine` in the composition root.
 */
export interface BattleEngineOverrides extends Partial<BattleEngineDependencies> {
  /** The catalog this engine plays on; never defaulted to ambient state. */
  readonly content: ContentCatalog;
}
