import type { GameEvent, GameState, StatusDef, StatusId, StatusModifiers, Unit } from './types';
import { type ContentCatalog } from './content-pack';
import { resolveDamage, type DamageRules } from './damage';
import { KeyedRegistry } from './registry';

const statusDef = (id: StatusId, content: ContentCatalog): StatusDef =>
  content.statuses.get(id);

export class StatusLifecycleContext {
  constructor(
    readonly rules: StatusRules,
    readonly state: GameState,
    readonly unit: Unit,
    readonly status: Unit['statuses'][number],
    readonly emit: (event: GameEvent) => void,
  ) {}

  /** Convenience for behaviours; the catalog always comes from the ruleset. */
  get content(): ContentCatalog {
    return this.rules.content;
  }

  damage(requested: number, nonlethal = false): number {
    return resolveDamage(this.rules, this.state, {
      unit: this.unit.id,
      amount: requested,
      nonlethal,
      report: (blow) => ({
        type: 'statusTick',
        unit: this.unit.id,
        status: this.status.id,
        amount: blow.amount,
        hpAfter: blow.hpAfter,
      }),
    }, this.emit).amount;
  }
}

export interface StatusBehavior {
  readonly id: StatusId;
  onOwnerTurnStart?(context: StatusLifecycleContext): void;
}

/** Optional imperative hooks for statuses whose behavior exceeds data modifiers. */
export class StatusBehaviorRegistry extends KeyedRegistry<StatusId, StatusBehavior> {
  constructor() {
    super('status behavior');
  }

  protected keyOf(behavior: StatusBehavior): StatusId {
    return behavior.id;
  }

  /** Most statuses are pure data, so having no behaviour is the normal case. */
  ownerTurnStart(context: StatusLifecycleContext): void {
    this.tryGet(context.status.id)?.onOwnerTurnStart?.(context);
  }

  clone(): StatusBehaviorRegistry {
    return this.copyInto(new StatusBehaviorRegistry());
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
  content: ContentCatalog,
  unit: Unit,
  id: StatusId,
  remaining: number,
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
export interface StatusRules extends DamageRules {
  readonly statusBehaviors: StatusBehaviorRegistry;
}

/** Resolves owner-turn-start periodic effects and duration expiry. */
export function resolveTurnStartStatuses(
  rules: StatusRules,
  state: GameState,
  emit: (event: GameEvent) => void,
  /** Units whose actor turn is starting. The caller decides the scope. */
  scope: readonly Unit[],
): void {
  const content = rules.content;
  for (const unit of scope) {
    for (const instance of [...unit.statuses]) {
      const def = statusDef(instance.id, content);
      const context = new StatusLifecycleContext(rules, state, unit, instance, emit);
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
