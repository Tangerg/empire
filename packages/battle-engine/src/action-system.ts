import { BattleAggregate } from './domain/battle-aggregate';
import {
  CombatModifierPipeline,
  CombatModifierProviders,
  DefaultCombatModifierPipeline,
} from './combat-modifiers';
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
import { type Registry } from './registry';
import { CoreTacticalSpace, DefaultTacticalSpace, type TacticalSpace } from './tactical-space';
import { TurnOrders, type TurnOrderPolicy } from './turn-order';
import { cloneContentCatalog, GlobalContentCatalog, type ContentCatalog } from './content-pack';
import type { Action, ActionKindMap, GameEvent, GameState } from './types';

export class IllegalActionError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'IllegalActionError';
  }
}

export type ActionKind = Extract<keyof ActionKindMap, string>;

/** Runtime rule dependencies shared by action, turn, scenario, and victory phases. */
export interface BattleRuleServices {
  readonly content: ContentCatalog;
  readonly abilities: Registry<AbilityDef>;
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
  readonly turnOrders: Registry<TurnOrderPolicy>;
}

/**
 * Prototype factory for an isolated ruleset. Mutable registries are cloned so
 * extending one engine cannot leak into another session.
 */
export function createDefaultBattleRuleServices(
  overrides: Partial<BattleRuleServices> = {},
): BattleRuleServices {
  const content = overrides.content ?? cloneContentCatalog(GlobalContentCatalog);
  return {
    content,
    abilities: overrides.abilities ?? Abilities.clone(),
    space: overrides.space ?? new DefaultTacticalSpace(content),
    combatModifiers:
      overrides.combatModifiers ?? new CombatModifierPipeline(CombatModifierProviders.clone()),
    hitEffects: overrides.hitEffects ?? WeaponHitEffectHandlers.clone(),
    statusBehaviors: overrides.statusBehaviors ?? StatusBehaviors.clone(),
    scenarioConditions: overrides.scenarioConditions ?? ScenarioConditionHandlers.clone(),
    scenarioEffects: overrides.scenarioEffects ?? ScenarioEffectHandlers.clone(),
    objectives: overrides.objectives ?? ObjectiveHandlers.clone(),
    progression: overrides.progression ?? DefaultRankProgression,
    resources: overrides.resources ?? DefaultBattleResources.clone(),
    turnOrders: overrides.turnOrders ?? TurnOrders.clone(),
  };
}

/**
 * Live global rules for low-level convenience reducers.
 *
 * Application-facing engines never use this object: their registries and
 * content are cloned by createDefaultBattleRuleServices. Keeping this global
 * façade live avoids capturing an empty content snapshot before packs install.
 */
export const DefaultBattleRuleServices: BattleRuleServices = {
  content: GlobalContentCatalog,
  abilities: Abilities,
  space: CoreTacticalSpace,
  combatModifiers: DefaultCombatModifierPipeline,
  hitEffects: WeaponHitEffectHandlers,
  statusBehaviors: StatusBehaviors,
  scenarioConditions: ScenarioConditionHandlers,
  scenarioEffects: ScenarioEffectHandlers,
  objectives: ObjectiveHandlers,
  progression: DefaultRankProgression,
  resources: DefaultBattleResources,
  turnOrders: TurnOrders,
};

export class ActionExecutionContext {
  readonly battle: BattleAggregate;
  readonly events: GameEvent[] = [];

  constructor(
    readonly state: GameState,
    readonly rules: BattleRuleServices,
  ) {
    this.battle = new BattleAggregate(state, rules.content);
  }

  readonly emit = (event: GameEvent): void => {
    this.events.push(event);
    this.state.scenario.eventCounts[event.type] = (this.state.scenario.eventCounts[event.type] ?? 0) + 1;
  };

  fail(message: string): never {
    throw new IllegalActionError(message);
  }
}

export interface ActionHandler<K extends ActionKind = ActionKind> {
  readonly kind: K;
  execute(context: ActionExecutionContext, action: ActionKindMap[K]): void;
}

/** Strategy registry: one cohesive handler per action kind. */
export class ActionHandlerRegistry {
  private readonly handlers = new Map<string, ActionHandler>();

  register<K extends ActionKind>(handler: ActionHandler<K>): this {
    if (this.handlers.has(handler.kind)) {
      throw new Error(`action handler already registered for "${handler.kind}"`);
    }
    this.handlers.set(handler.kind, handler as ActionHandler);
    return this;
  }

  replace<K extends ActionKind>(handler: ActionHandler<K>): this {
    this.handlers.set(handler.kind, handler as ActionHandler);
    return this;
  }

  handler(kind: ActionKind): ActionHandler {
    const handler = this.handlers.get(kind);
    if (!handler) throw new IllegalActionError(`没有处理行动类型「${kind}」的规则策略`);
    return handler;
  }

  dispatch(context: ActionExecutionContext, action: Action): void {
    // The kind lookup proves the runtime pair. The erased call is isolated here
    // so individual handlers remain fully typed.
    this.handler(action.kind).execute(context, action as never);
  }

  kinds(): string[] {
    return [...this.handlers.keys()];
  }

  clone(): ActionHandlerRegistry {
    const copy = new ActionHandlerRegistry();
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }
}
