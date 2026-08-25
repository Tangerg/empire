import { addStatus, removeStatus } from './statuses';
import { forceMoveUnit } from './forced-movement';
import type {
  GameEvent,
  GameState,
  Unit,
  WeaponHitEffect,
  WeaponHitEffectKindMap,
} from './types';
import { type ContentCatalog } from './content-pack';
import { KeyedRegistry } from './registry';
import { type UnitDepartureRules } from './unit-departure';

type HitEffectKind = Extract<keyof WeaponHitEffectKindMap, string>;

export class WeaponHitEffectContext {
  constructor(
    /** The whole ruleset: an effect may reach any rule the engine composes. */
    readonly rules: WeaponHitEffectRules,
    readonly state: GameState,
    readonly attacker: Unit,
    readonly target: Unit,
    readonly emit: (event: GameEvent) => void,
  ) {}

  get content(): ContentCatalog {
    return this.rules.content;
  }
}

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export type WeaponHitEffectRules = UnitDepartureRules;

export interface WeaponHitEffectHandler<K extends HitEffectKind = HitEffectKind> {
  readonly kind: K;
  apply(context: WeaponHitEffectContext, effect: WeaponHitEffectKindMap[K]): void;
  describe(effect: WeaponHitEffectKindMap[K]): string;
}

/** Open effect algebra used by weapons, combat plans, UI and content packs. */
export class WeaponHitEffectHandlerRegistry extends KeyedRegistry<string, WeaponHitEffectHandler> {
  constructor() {
    super('weapon hit effect');
  }

  protected keyOf(handler: WeaponHitEffectHandler): string {
    return handler.kind;
  }

  override register<K extends HitEffectKind>(handler: WeaponHitEffectHandler<K>): this {
    return super.register(handler as WeaponHitEffectHandler);
  }

  override replace<K extends HitEffectKind>(handler: WeaponHitEffectHandler<K>): this {
    return super.replace(handler as WeaponHitEffectHandler);
  }

  apply(
    rules: WeaponHitEffectRules,
    state: GameState,
    attacker: Unit,
    target: Unit,
    effects: WeaponHitEffect[],
    emit: (event: GameEvent) => void,
  ): void {
    const context = new WeaponHitEffectContext(rules, state, attacker, target, emit);
    for (const effect of effects) {
      this.get(effect.type).apply(context, effect as never);
    }
  }

  describe(effect: WeaponHitEffect): string {
    return this.get(effect.type).describe(effect as never);
  }

  clone(): WeaponHitEffectHandlerRegistry {
    return this.copyInto(new WeaponHitEffectHandlerRegistry());
  }
}

export const WeaponHitEffectHandlers = new WeaponHitEffectHandlerRegistry()
  .register({
    kind: 'addStatus',
    apply: ({ target, attacker, emit, content }, effect) => {
      addStatus(content, target, { id: effect.status, remaining: effect.duration, sourceUnitId: attacker.id }, emit);
    },
    describe: (effect) => `施加 ${effect.status}（${effect.duration} 回合）`,
  })
  .register({
    kind: 'removeStatus',
    apply: ({ target, emit }, effect) => {
      removeStatus(target, effect.status, emit);
    },
    describe: (effect) => `移除 ${effect.status}`,
  })
  .register({
    kind: 'forcedMove',
    apply: ({ rules, state, attacker, target, emit }, effect) => {
      forceMoveUnit(rules, state, {
        unit: target.id,
        source: attacker,
        mode: effect.mode,
        distance: effect.distance,
        collisionDamage: effect.collisionDamage,
      }, emit);
    },
    describe: (effect) => `${effect.mode === 'push' ? '击退' : '拉拽'} ${effect.distance} 格`,
  })
  .seal();
