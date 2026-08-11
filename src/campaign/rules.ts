import type {
  CampaignCondition,
  CampaignConditionKindMap,
  CampaignEffect,
  CampaignEffectKindMap,
  CampaignState,
} from './types';

type ConditionKind = Extract<keyof CampaignConditionKindMap, string>;
type EffectKind = Extract<keyof CampaignEffectKindMap, string>;

function compare(left: unknown, op: 'eq' | 'neq' | 'gte' | 'lte', right: unknown): boolean {
  if (op === 'eq') return left === right;
  if (op === 'neq') return left !== right;
  if (typeof left !== 'number' || typeof right !== 'number') return false;
  return op === 'gte' ? left >= right : left <= right;
}

export interface CampaignConditionHandler<K extends ConditionKind = ConditionKind> {
  kind: K;
  evaluate(state: CampaignState, condition: CampaignConditionKindMap[K], rules: CampaignConditionRegistry): boolean;
}

export class CampaignConditionRegistry {
  private readonly handlers = new Map<string, CampaignConditionHandler>();

  register<K extends ConditionKind>(handler: CampaignConditionHandler<K>): this {
    if (this.handlers.has(handler.kind)) throw new Error(`campaign condition handler already registered: "${handler.kind}"`);
    this.handlers.set(handler.kind, handler as CampaignConditionHandler);
    return this;
  }

  evaluate(state: CampaignState, condition: CampaignCondition): boolean {
    const handler = this.handlers.get(condition.type);
    if (!handler) throw new Error(`no campaign condition handler for "${condition.type}"`);
    return handler.evaluate(state, condition as never, this);
  }

  clone(): CampaignConditionRegistry {
    const copy = new CampaignConditionRegistry();
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }
}

const condition = <K extends ConditionKind>(
  kind: K,
  evaluate: CampaignConditionHandler<K>['evaluate'],
): CampaignConditionHandler<K> => ({ kind, evaluate });

export const DefaultCampaignConditions = new CampaignConditionRegistry()
  .register(condition('flag', (state, value) => state.flags.includes(value.flag) === (value.present ?? true)))
  .register(condition('variable', (state, value) => compare(state.variables[value.key], value.op, value.value)))
  .register(condition('resource', (state, value) => compare(state.resources[value.resource] ?? 0, value.op, value.value)))
  .register(condition('relation', (state, value) => compare(state.relations[value.faction] ?? 0, value.op, value.value)))
  .register(condition('roster', (state, value) => {
    const unit = state.roster[value.unit];
    return Boolean(unit) && (!value.disposition || unit.disposition === value.disposition);
  }))
  .register(condition('all', (state, value, rules) => value.conditions.every((child) => rules.evaluate(state, child))))
  .register(condition('any', (state, value, rules) => value.conditions.some((child) => rules.evaluate(state, child))))
  .register(condition('not', (state, value, rules) => !rules.evaluate(state, value.condition)));

export interface CampaignEffectHandler<K extends EffectKind = EffectKind> {
  kind: K;
  apply(state: CampaignState, effect: CampaignEffectKindMap[K]): void;
}

export class CampaignEffectRegistry {
  private readonly handlers = new Map<string, CampaignEffectHandler>();

  register<K extends EffectKind>(handler: CampaignEffectHandler<K>): this {
    if (this.handlers.has(handler.kind)) throw new Error(`campaign effect handler already registered: "${handler.kind}"`);
    this.handlers.set(handler.kind, handler as CampaignEffectHandler);
    return this;
  }

  apply(state: CampaignState, effect: CampaignEffect): void {
    const handler = this.handlers.get(effect.type);
    if (!handler) throw new Error(`no campaign effect handler for "${effect.type}"`);
    handler.apply(state, effect as never);
  }

  clone(): CampaignEffectRegistry {
    const copy = new CampaignEffectRegistry();
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }
}

const effect = <K extends EffectKind>(
  kind: K,
  apply: CampaignEffectHandler<K>['apply'],
): CampaignEffectHandler<K> => ({ kind, apply });

export const DefaultCampaignEffects = new CampaignEffectRegistry()
  .register(effect('setVariable', (state, value) => { state.variables[value.key] = value.value; }))
  .register(effect('addVariable', (state, value) => {
    const current = state.variables[value.key] ?? 0;
    if (typeof current !== 'number') throw new Error(`campaign variable "${value.key}" is not numeric`);
    state.variables[value.key] = current + value.amount;
  }))
  .register(effect('setFlag', (state, value) => {
    if (!state.flags.includes(value.flag)) state.flags.push(value.flag);
  }))
  .register(effect('clearFlag', (state, value) => {
    state.flags = state.flags.filter((flag) => flag !== value.flag);
  }))
  .register(effect('addResource', (state, value) => {
    state.resources[value.resource] = (state.resources[value.resource] ?? 0) + value.amount;
  }))
  .register(effect('changeRelation', (state, value) => {
    state.relations[value.faction] = (state.relations[value.faction] ?? 0) + value.amount;
  }))
  .register(effect('setFeature', (state, value) => {
    state.features = value.enabled
      ? [...new Set([...state.features, value.feature])]
      : state.features.filter((feature) => feature !== value.feature);
  }))
  .register(effect('setRosterDisposition', (state, value) => {
    const unit = state.roster[value.unit];
    if (!unit) throw new Error(`unknown campaign roster unit "${value.unit}"`);
    unit.disposition = value.disposition;
  }));

export interface CampaignRuleServices {
  conditions: CampaignConditionRegistry;
  effects: CampaignEffectRegistry;
}

export function createCampaignRuleServices(overrides: Partial<CampaignRuleServices> = {}): CampaignRuleServices {
  return {
    conditions: overrides.conditions ?? DefaultCampaignConditions.clone(),
    effects: overrides.effects ?? DefaultCampaignEffects.clone(),
  };
}
