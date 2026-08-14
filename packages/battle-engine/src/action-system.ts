import { BattleAggregate } from './domain/battle-aggregate';
import { boardOf, type Board } from './domain/board';
import { IllegalActionError } from './domain/errors';
import { UnitEntity } from './domain/unit-entity';
import { BattleLifecycle } from './turn-cycle';
import type { CombatModifierPipeline } from './combat-modifiers';
import type { WeaponHitEffectHandlerRegistry } from './hit-effects';
import type { ObjectiveHandlerRegistry } from './objective-system';
import type { RankProgressionPolicy } from './progression';
import type {
  ScenarioConditionHandlerRegistry,
  ScenarioEffectHandlerRegistry,
} from './scenario';
import type { StatusBehaviorRegistry } from './statuses';
import type { BattleResourceSystem } from './resources';
import type { AbilityDef } from './abilities';
import { type ContentRegistry, KeyedRegistry } from './registry';
import type { TacticalSpace } from './tactical-space';
import { activeTurnOrder, mayAct, type TurnOrderPolicy } from './turn-order';
import type { ReactionBehavior } from './reactions';
import type { UnitDepartureHandlerRegistry } from './unit-departure';
import type { WeaponAreaShapeRegistry } from './weapon-area';
import type { UnitDirectiveBehavior } from './unit-directive';
import type { RuleReferenceCheckRegistry } from './rule-references';
import type { GridRegistry } from './tactical-grid';
import type { BattleSaveMigrator } from './battle-save';
import type { RandomSource } from './random';
import { type ContentCatalog } from './content-pack';
import type { Action, ActionKindMap, Direction, GameEvent, GameState } from './types';

export { IllegalActionError };

export type ActionKind = Extract<keyof ActionKindMap, string>;

/** Runtime rule dependencies shared by action, turn, scenario, and victory phases. */
export interface BattleRuleServices {
  readonly content: ContentCatalog;
  readonly abilities: ContentRegistry<AbilityDef>;
  readonly space: TacticalSpace;
  readonly combatModifiers: CombatModifierPipeline;
  readonly hitEffects: WeaponHitEffectHandlerRegistry;
  readonly statusBehaviors: StatusBehaviorRegistry;
  readonly scenarioConditions: ScenarioConditionHandlerRegistry;
  readonly scenarioEffects: ScenarioEffectHandlerRegistry;
  readonly objectives: ObjectiveHandlerRegistry;
  readonly progression: RankProgressionPolicy;
  readonly resources: BattleResourceSystem;
  /** Registered turn-order policies; the level's ruleset selects one by id. */
  readonly turnOrders: ContentRegistry<TurnOrderPolicy>;
  /** Registered reaction stances; a unit's `reaction` selects one by id. */
  readonly reactions: ContentRegistry<ReactionBehavior>;
  /** Consequences of a unit leaving the field, in registration order. */
  readonly unitDepartures: UnitDepartureHandlerRegistry;
  /** Seeded randomness. Swap for DeterministicOnlyRandom to forbid variance. */
  readonly random: RandomSource;
  /** Blast shapes a weapon's `area` may name. */
  readonly areaShapes: WeaponAreaShapeRegistry;
  /** Standing orders a unit's `directive.mode` may name. */
  readonly directives: ContentRegistry<UnitDirectiveBehavior>;
  /** Registered tilings; the level's ruleset selects one by id. */
  readonly grids: GridRegistry;
  /** Which names in a catalog, a level or a save this ruleset has to implement. */
  readonly referenceChecks: RuleReferenceCheckRegistry;
  /** How a battle written to disk is read back: schema ladder plus refusals. */
  readonly saves: BattleSaveMigrator;
}

export class ActionExecutionContext {
  readonly battle: BattleAggregate;
  /** Phase and round transitions this order may trigger. */
  readonly lifecycle: BattleLifecycle;
  /** The board this order is given on, under the tiling the level named. */
  readonly board: Board;
  readonly events: GameEvent[] = [];

  constructor(
    readonly state: GameState,
    readonly rules: BattleRuleServices,
  ) {
    this.battle = new BattleAggregate(state, rules.content);
    this.board = boardOf(rules, state);
    this.lifecycle = new BattleLifecycle(state, rules, this.emit);
  }

  readonly emit = (event: GameEvent): void => {
    this.events.push(event);
    this.state.scenario.eventCounts[event.type] = (this.state.scenario.eventCounts[event.type] ?? 0) + 1;
  };

  fail(message: string): never {
    throw new IllegalActionError(message);
  }

  /** The ordering policy this battle is running under. */
  get turnOrder(): TurnOrderPolicy {
    return activeTurnOrder(this.rules, this.state);
  }

  /**
   * Resolves a unit an order names, refusing an order aimed at nobody.
   *
   * A client naming a unit that already died is issuing a bad *order*, not
   * tripping an engine invariant, so it must surface as a refusal.
   */
  unit(unitId: number): UnitEntity {
    const unit = this.battle.findUnit(unitId);
    if (!unit) this.fail(`单位 ${unitId} 不在场上`);
    return unit;
  }

  /** Resolves a unit the acting player owns, whatever its entitlement. */
  ownUnit(unitId: number): UnitEntity {
    const unit = this.unit(unitId);
    if (!unit.isOwnedBy(this.state.currentPlayer)) this.fail('不是你的单位');
    return unit;
  }

  /**
   * Resolves the unit an order names *and* asserts the right to order it now.
   *
   * Ownership, the spent-action flag and the ordering policy's entitlement are
   * one rule, not three checks every handler has to remember. Handlers that
   * only checked the first two were right by accident under side turns, where
   * "mine and not yet done" happens to coincide with "entitled to act"; under an
   * initiative order they let a player retask their whole army during one
   * unit's turn.
   *
   * `order` names the refused order so the message says what was denied.
   */
  commandableUnit(unitId: number, order: string): UnitEntity {
    const unit = this.ownUnit(unitId);
    if (unit.hasActed) this.fail(`已行动单位不能${order}`);
    if (!mayAct(this.rules, this.state, unit.state)) this.fail(`该单位当前没有行动权，不能${order}`);
    return unit;
  }

  /** Turns a unit, announcing it only when the facing actually changed. */
  turnToFace(unit: UnitEntity, facing: Direction): void {
    const previous = unit.changeFacing(facing);
    if (previous === facing) return;
    this.emit({ type: 'facingChanged', unit: unit.id, from: previous, to: facing });
  }
}

export interface ActionHandler<K extends ActionKind = ActionKind> {
  readonly kind: K;
  /**
   * This order hands the turn to somebody else, so nothing before it can be
   * taken back.
   *
   * Declared by the handler because it is a fact about the order. The session
   * shell used to compare the action kind against a closed pair of names, so a
   * rule pack's own "sound the retreat" action would have been offered an undo
   * that rewound past a turn boundary.
   */
  readonly handsOffTurn?: boolean;
  execute(context: ActionExecutionContext, action: ActionKindMap[K]): void;
}

/** Strategy registry: one cohesive handler per action kind. */
export class ActionHandlerRegistry extends KeyedRegistry<ActionKind, ActionHandler> {
  constructor() {
    super('action handler');
  }

  protected keyOf(handler: ActionHandler): ActionKind {
    return handler.kind;
  }

  override register<K extends ActionKind>(handler: ActionHandler<K>): this {
    return super.register(handler as ActionHandler);
  }

  override replace<K extends ActionKind>(handler: ActionHandler<K>): this {
    return super.replace(handler as ActionHandler);
  }

  /** Whether this order ends the current turn's scope. Unknown kinds do not. */
  handsOffTurn(action: Action): boolean {
    return this.tryGet(action.kind)?.handsOffTurn ?? false;
  }

  dispatch(context: ActionExecutionContext, action: Action): void {
    const handler = this.tryGet(action.kind);
    // An unknown kind reaches here from a save file or a mod, so it is a refused
    // order rather than a defect: the taxonomy answer differs from the base's.
    if (!handler) throw new IllegalActionError(`没有处理行动类型「${action.kind}」的规则策略`);
    // The kind lookup proves the runtime pair. The erased call is isolated here
    // so individual handlers remain fully typed.
    handler.execute(context, action as never);
  }

  clone(): ActionHandlerRegistry {
    return this.copyInto(new ActionHandlerRegistry());
  }
}
