import { BattleAggregate } from './domain/battle-aggregate';
import type { GameEvent, GameState, StatusDef, StatusId, StatusModifiers, Unit } from './types';
import { type ContentCatalog } from './content-pack';
import { resolveMoraleAfterDamage } from './morale';
import { emitTransportLossEvents } from './transports';

export const statusDef = (id: StatusId, content: ContentCatalog): StatusDef =>
  content.statuses.get(id);

export class StatusLifecycleContext {
  constructor(
    readonly state: GameState,
    readonly unit: Unit,
    readonly status: Unit['statuses'][number],
    readonly emit: (event: GameEvent) => void,
    readonly content: ContentCatalog,
    private readonly onDeath?: (unitId: number) => void,
  ) {}

  damage(requested: number, nonlethal = false): number {
    if (!this.state.units.some((candidate) => candidate.id === this.unit.id)) return 0;
    const floor = nonlethal ? 1 : 0;
    const amount = Math.min(Math.max(0, Math.round(requested)), Math.max(0, this.unit.hp - floor));
    if (amount <= 0) return 0;
    const result = new BattleAggregate(this.state, this.content).damageUnit(this.unit.id, amount);
    this.emit({
      type: 'statusTick',
      unit: this.unit.id,
      status: this.status.id,
      amount: result.amount,
      hpAfter: result.hpAfter,
    });
    if (result.killed) {
      this.emit({ type: 'death', unit: this.unit.id, at: result.at });
      if (result.marker) this.emit({ type: 'markerAdded', marker: result.marker.id, kind: result.marker.kind, at: result.marker.at });
      this.onDeath?.(this.unit.id);
      emitTransportLossEvents(this.unit.id, result.at, result.passengerMarkers, this.emit);
      resolveMoraleAfterDamage(this.state, this.unit, result.amount, true, result.at, this.emit, this.content);
    } else if (resolveMoraleAfterDamage(this.state, this.unit, result.amount, false, result.at, this.emit, this.content)) {
      this.onDeath?.(this.unit.id);
    }
    return result.amount;
  }
}

export interface StatusBehavior {
  readonly id: StatusId;
  onOwnerTurnStart?(context: StatusLifecycleContext): void;
}

/** Optional imperative hooks for statuses whose behavior exceeds data modifiers. */
export class StatusBehaviorRegistry {
  private readonly behaviors = new Map<StatusId, StatusBehavior>();

  register(behavior: StatusBehavior): this {
    if (this.behaviors.has(behavior.id)) throw new Error(`status behavior already registered: "${behavior.id}"`);
    this.behaviors.set(behavior.id, behavior);
    return this;
  }

  replace(behavior: StatusBehavior): this {
    this.behaviors.set(behavior.id, behavior);
    return this;
  }

  ownerTurnStart(context: StatusLifecycleContext): void {
    this.behaviors.get(context.status.id)?.onOwnerTurnStart?.(context);
  }

  clone(): StatusBehaviorRegistry {
    const copy = new StatusBehaviorRegistry();
    for (const behavior of this.behaviors.values()) copy.register(behavior);
    return copy;
  }
}

export const StatusBehaviors = new StatusBehaviorRegistry();

export function blockedAbilityStatus(
  unit: Unit,
  tags: string[],
  content: ContentCatalog,
): StatusId | null {
  if (tags.length === 0) return null;
  for (const instance of unit.statuses) {
    const blocked = statusDef(instance.id, content).blockedAbilityTags;
    if (blocked.some((tag) => tags.includes(tag))) return instance.id;
  }
  return null;
}

export function combinedStatusModifiers(
  unit: Unit,
  content: ContentCatalog,
): Required<Omit<StatusModifiers, 'cannotCapture'>> & {
  cannotCapture: boolean;
} {
  let attackMultiplier = 1;
  let defenseDelta = 0;
  let movementDelta = 0;
  let cannotCapture = false;
  for (const instance of unit.statuses) {
    const modifiers = statusDef(instance.id, content).modifiers;
    const stacks = Math.max(1, instance.stacks);
    if (modifiers.attackMultiplier !== undefined) {
      attackMultiplier *= Math.pow(modifiers.attackMultiplier, stacks);
    }
    defenseDelta += (modifiers.defenseDelta ?? 0) * stacks;
    movementDelta += (modifiers.movementDelta ?? 0) * stacks;
    cannotCapture ||= modifiers.cannotCapture ?? false;
  }
  return { attackMultiplier, defenseDelta, movementDelta, cannotCapture };
}

export function hasStatus(unit: Unit, id: StatusId): boolean {
  return unit.statuses.some((instance) => instance.id === id);
}

export function addStatus(
  unit: Unit,
  id: StatusId,
  remaining: number,
  content: ContentCatalog,
  emit?: (event: GameEvent) => void,
  sourceUnitId?: number,
): void {
  if (!Number.isInteger(remaining) || remaining < 1) throw new Error('status duration must be >= 1');
  const def = statusDef(id, content);
  const current = unit.statuses.find((instance) => instance.id === id);
  if (!current) {
    unit.statuses.push({ id, remaining, stacks: 1, sourceUnitId });
  } else {
    switch (def.stackMode) {
      case 'refresh':
        current.remaining = Math.max(current.remaining, remaining);
        break;
      case 'extend':
        current.remaining += remaining;
        break;
      case 'stack':
        current.stacks = Math.min(def.maxStacks, current.stacks + 1);
        current.remaining = Math.max(current.remaining, remaining);
        break;
    }
    if (sourceUnitId !== undefined) current.sourceUnitId = sourceUnitId;
  }
  const applied = unit.statuses.find((instance) => instance.id === id)!;
  emit?.({
    type: 'statusApplied',
    unit: unit.id,
    status: id,
    remaining: applied.remaining,
    stacks: applied.stacks,
  });
}

export function removeStatus(
  unit: Unit,
  id: StatusId,
  emit?: (event: GameEvent) => void,
): boolean {
  const index = unit.statuses.findIndex((instance) => instance.id === id);
  if (index < 0) return false;
  unit.statuses.splice(index, 1);
  emit?.({ type: 'statusRemoved', unit: unit.id, status: id });
  return true;
}

/**
 * Port declared by this module. The composition-level `BattleRuleServices`
 * satisfies it structurally, so neither side needs to import the other.
 */
export interface StatusRules {
  readonly content: ContentCatalog;
  readonly statusBehaviors: StatusBehaviorRegistry;
}

/** Resolves owner-turn-start periodic effects and duration expiry. */
export function resolveTurnStartStatuses(
  rules: StatusRules,
  state: GameState,
  emit: (event: GameEvent) => void,
  /** Units whose actor turn is starting. The caller decides the scope. */
  scope: readonly Unit[],
  onDeath?: (unitId: number) => void,
): void {
  const content = rules.content;
  for (const unit of scope) {
    for (const instance of [...unit.statuses]) {
      const def = statusDef(instance.id, content);
      const context = new StatusLifecycleContext(state, unit, instance, emit, content, onDeath);
      if (def.periodic?.timing === 'ownerTurnStart') {
        const maxHp = content.units.get(unit.type).maxHp;
        const requested = Math.max(1, Math.round(maxHp * def.periodic.maxHpFraction * instance.stacks));
        context.damage(requested, def.periodic.nonlethal);
        if (!state.units.some((candidate) => candidate.id === unit.id)) break;
      }
      rules.statusBehaviors.ownerTurnStart(context);
      if (!state.units.some((candidate) => candidate.id === unit.id)) break;
      instance.remaining--;
      if (instance.remaining <= 0 && state.units.some((candidate) => candidate.id === unit.id)) {
        removeStatus(unit, instance.id, emit);
      }
    }
  }
}
