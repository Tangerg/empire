import type { GameEvent, Unit, UnitRank } from './types';
import { UnitEntity } from './domain/unit-entity';
import { awardCareerProgress } from './careers';
import {
  type BattleResourceSystem,
  MOMENTUM_RESOURCE,
  unitResource,
} from './resources';
import { type ContentCatalog } from './content-pack';

export interface RankProgressionPolicy {
  rankFor(progress: number): UnitRank;
  nextThreshold(rank: UnitRank): number | null;
}

/** Thresholds are deliberately battle-local and compact to limit snowballing. */
export class ThresholdRankProgressionPolicy implements RankProgressionPolicy {
  constructor(readonly veteranThreshold = 120, readonly eliteThreshold = 320) {
    if (veteranThreshold <= 0 || eliteThreshold <= veteranThreshold) {
      throw new Error('rank thresholds must be positive and strictly increasing');
    }
  }

  rankFor(progress: number): UnitRank {
    if (progress >= this.eliteThreshold) return 2;
    if (progress >= this.veteranThreshold) return 1;
    return 0;
  }

  nextThreshold(rank: UnitRank): number | null {
    if (rank === 0) return this.veteranThreshold;
    if (rank === 1) return this.eliteThreshold;
    return null;
  }
}

export const DefaultRankProgression = new ThresholdRankProgressionPolicy();

export function awardRankProgress(
  unit: Unit,
  requested: number,
  emit: (event: GameEvent) => void,
  policy: RankProgressionPolicy,
  content: ContentCatalog,
): void {
  const entity = new UnitEntity(unit);
  const gained = entity.addRankProgress(requested);
  if (gained <= 0) return;
  awardCareerProgress(unit, gained, emit, content);
  emit({ type: 'rankProgressChanged', unit: unit.id, amount: gained, current: unit.rankProgress });
  const next = policy.rankFor(unit.rankProgress);
  if (next <= unit.rank) return;
  const from = entity.changeRank(next);
  emit({ type: 'rankChanged', unit: unit.id, from, to: next });
}

export function changeMomentum(
  unit: Unit,
  requested: number,
  emit: (event: GameEvent) => void,
  resources: BattleResourceSystem,
): void {
  changeUnitResource(unit, MOMENTUM_RESOURCE, requested, emit, resources);
}

/** Changes any unit-scoped account without exposing its storage to mechanics. */
export function changeUnitResource(
  unit: Unit,
  resource: string,
  requested: number,
  emit: (event: GameEvent) => void,
  resources: BattleResourceSystem,
): void {
  const subject = unitResource(unit);
  if (!resources.hasAccount(resource, subject) || requested === 0) return;
  const balance = resources.balance(resource, subject);
  const amount = requested > 0
    ? resources.credit(resource, subject, requested)
    : -resources.spend(resource, subject, Math.min(-requested, balance ?? 0));
  const current = resources.balance(resource, subject);
  if (amount !== 0 && current !== null) {
    emit({
      type: 'resourceChanged',
      resource,
      subject: { kind: 'unit', id: unit.id },
      amount,
      current,
    });
  }
}

/**
 * Per-battle progression hook. Campaign code may convert the final value into
 * persistent XP, but combat resolution never reads a save file directly.
 */
export function awardCombatProgress(
  unit: Unit,
  damage: number,
  defeatedTarget: boolean,
  emit: (event: GameEvent) => void,
  policy: RankProgressionPolicy,
  content: ContentCatalog,
): void {
  const amount = Math.max(1, Math.round(damage)) + (defeatedTarget ? 50 : 0);
  awardRankProgress(unit, amount, emit, policy, content);
}

export function awardDamageTakenMomentum(
  unit: Unit,
  emit: (event: GameEvent) => void,
  resources: BattleResourceSystem,
): void {
  changeMomentum(unit, 3, emit, resources);
}
