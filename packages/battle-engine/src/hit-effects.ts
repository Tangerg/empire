import { addStatus, removeStatus } from './statuses';
import { forceMoveUnit } from './forced-movement';
import type {
  GameEvent,
  GameState,
  Unit,
  WeaponHitEffect,
  WeaponHitEffectKindMap,
} from './types';
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';

type HitEffectKind = Extract<keyof WeaponHitEffectKindMap, string>;

export class WeaponHitEffectContext {
  constructor(
    readonly state: GameState,
    readonly attacker: Unit,
    readonly target: Unit,
    readonly emit: (event: GameEvent) => void,
    readonly content: ContentCatalog,
  ) {}
}

export interface WeaponHitEffectHandler<K extends HitEffectKind = HitEffectKind> {
  readonly kind: K;
  apply(context: WeaponHitEffectContext, effect: WeaponHitEffectKindMap[K]): void;
  describe(effect: WeaponHitEffectKindMap[K]): string;
}

/** Open effect algebra used by weapons, combat plans, UI and content packs. */
export class WeaponHitEffectHandlerRegistry {
  private readonly handlers = new Map<string, WeaponHitEffectHandler>();

  register<K extends HitEffectKind>(handler: WeaponHitEffectHandler<K>): this {
    if (this.handlers.has(handler.kind)) throw new Error(`weapon hit effect already registered: "${handler.kind}"`);
    this.handlers.set(handler.kind, handler as WeaponHitEffectHandler);
    return this;
  }

  replace<K extends HitEffectKind>(handler: WeaponHitEffectHandler<K>): this {
    this.handlers.set(handler.kind, handler as WeaponHitEffectHandler);
    return this;
  }

  apply(
    state: GameState,
    attacker: Unit,
    target: Unit,
    effects: WeaponHitEffect[],
    emit: (event: GameEvent) => void,
    content: ContentCatalog = GlobalContentCatalog,
  ): void {
    const context = new WeaponHitEffectContext(state, attacker, target, emit, content);
    for (const effect of effects) {
      const handler = this.handlers.get(effect.type);
      if (!handler) throw new Error(`no weapon hit effect handler for "${effect.type}"`);
      handler.apply(context, effect as never);
    }
  }

  describe(effect: WeaponHitEffect): string {
    const handler = this.handlers.get(effect.type);
    if (!handler) throw new Error(`no weapon hit effect handler for "${effect.type}"`);
    return handler.describe(effect as never);
  }

  clone(): WeaponHitEffectHandlerRegistry {
    const copy = new WeaponHitEffectHandlerRegistry();
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }

  kinds(): string[] {
    return [...this.handlers.keys()];
  }
}

export const WeaponHitEffectHandlers = new WeaponHitEffectHandlerRegistry()
  .register({
    kind: 'addStatus',
    apply: ({ target, attacker, emit, content }, effect) => {
      addStatus(target, effect.status, effect.duration, emit, attacker.id, content);
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
    apply: ({ state, attacker, target, emit, content }, effect) => {
      forceMoveUnit(state, {
        unit: target.id,
        source: attacker,
        mode: effect.mode,
        distance: effect.distance,
        collisionDamage: effect.collisionDamage,
      }, emit, content);
    },
    describe: (effect) => `${effect.mode === 'push' ? '击退' : '拉拽'} ${effect.distance} 格`,
  });
