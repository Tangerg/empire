import { BattleAggregate } from './domain/battle-aggregate';
import { IllegalActionError } from './domain/errors';
import { UnitEntity } from './domain/unit-entity';
import { BattleLifecycle } from './turn-cycle';
import { CombatModifierPipeline, CombatModifierProviders } from './combat-modifiers';
import { WeaponHitEffectHandlers, type WeaponHitEffectHandlerRegistry } from './hit-effects';
import { ObjectiveHandlers, type ObjectiveHandlerRegistry } from './objective-system';
import { DefaultRankProgression, type RankProgressionPolicy } from './progression';
import {
  ScenarioConditionHandlers,
  ScenarioEffectHandlers,
  type ScenarioConditionHandlerRegistry,
  type ScenarioEffectHandlerRegistry,
} from './scenario';
import { StatusBehaviors, type StatusBehaviorRegistry } from './statuses';
import { DefaultBattleResources, type BattleResourceSystem } from './resources';
import { Abilities, type AbilityDef } from './abilities';
import { type ContentRegistry, KeyedRegistry } from './registry';
import { DefaultTacticalSpace, type TacticalSpace } from './tactical-space';
import { activeTurnOrder, mayAct, TurnOrders, type TurnOrderPolicy } from './turn-order';
import { Reactions, type ReactionBehavior } from './reactions';
import { UnitDepartureHandlers, type UnitDepartureHandlerRegistry } from './unit-departure';
import { WeaponAreaShapes, type WeaponAreaShapeRegistry } from './weapon-area';
import { SplitMixRandom, type RandomSource } from './random';
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
}

/**
 * Prototype factory for an isolated ruleset. Mutable registries are cloned so
 * extending one engine cannot leak into another session.
 *
 * `content` is required: a ruleset without a declared catalog used to fall back
 * to ambient state, which made engine instances silently share content.
 */
export interface BattleRuleServiceOverrides extends Partial<BattleRuleServices> {
  readonly content: ContentCatalog;
}

export function createDefaultBattleRuleServices(
  overrides: BattleRuleServiceOverrides,
): BattleRuleServices {
  const content = overrides.content;
  const random = overrides.random ?? SplitMixRandom;
  return {
    random,
    content,
    abilities: overrides.abilities ?? Abilities.clone(),
    space: overrides.space ?? new DefaultTacticalSpace(content),
    combatModifiers:
      overrides.combatModifiers ?? new CombatModifierPipeline(CombatModifierProviders.clone()),
    hitEffects: overrides.hitEffects ?? WeaponHitEffectHandlers.clone(),
    statusBehaviors: overrides.statusBehaviors ?? StatusBehaviors.clone(),
    scenarioConditions: overrides.scenarioConditions ?? ScenarioConditionHandlers.clone(random),
    scenarioEffects: overrides.scenarioEffects ?? ScenarioEffectHandlers.clone(),
    objectives: overrides.objectives ?? ObjectiveHandlers.clone(),
    progression: overrides.progression ?? DefaultRankProgression,
    resources: overrides.resources ?? DefaultBattleResources.clone(),
    turnOrders: overrides.turnOrders ?? TurnOrders.clone(),
    reactions: overrides.reactions ?? Reactions.clone(),
    unitDepartures: overrides.unitDepartures ?? UnitDepartureHandlers.clone(),
    areaShapes: overrides.areaShapes ?? WeaponAreaShapes.clone(),
  };
}


export class ActionExecutionContext {
  readonly battle: BattleAggregate;
  /** Phase and round transitions this order may trigger. */
  readonly lifecycle: BattleLifecycle;
  readonly events: GameEvent[] = [];

  constructor(
    readonly state: GameState,
    readonly rules: BattleRuleServices,
  ) {
    this.battle = new BattleAggregate(state, rules.content);
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
