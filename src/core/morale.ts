import { dist } from './grid';
import { removeUnit, requireUnit } from './state';
import type { ContentCatalog } from './content-pack';
import { GlobalContentCatalog } from './content-pack';
import type { BattlefieldMarker, GameEvent, GameState, PlayerId, Unit } from './types';
import { cloneUnitState } from './unit-state';

function withdrawalMarker(state: GameState, unit: Unit, kind: string, meta: BattlefieldMarker['meta']): BattlefieldMarker {
  const marker: BattlefieldMarker = {
    id: state.nextMarkerId++,
    kind,
    at: { x: unit.x, y: unit.y },
    owner: unit.owner,
    fallenUnit: cloneUnitState(unit),
    meta,
  };
  state.markers.push(marker);
  removeUnit(state, unit.id);
  return marker;
}

export function routeUnit(state: GameState, unitId: number, emit: (event: GameEvent) => void): BattlefieldMarker {
  const unit = requireUnit(state, unitId);
  const marker = withdrawalMarker(state, unit, 'routed', {});
  emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({ type: 'unitRouted', unit: unitId, marker: marker.id, at: marker.at });
  return marker;
}

export function surrenderUnit(
  state: GameState,
  unitId: number,
  to: PlayerId | undefined,
  emit: (event: GameEvent) => void,
): BattlefieldMarker {
  const unit = requireUnit(state, unitId);
  const marker = withdrawalMarker(state, unit, 'surrendered', to === undefined ? {} : { surrenderedTo: to });
  emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({ type: 'unitSurrendered', unit: unitId, marker: marker.id, at: marker.at, to });
  return marker;
}

export function changeMorale(
  state: GameState,
  unitId: number,
  requested: number,
  reason: string,
  emit: (event: GameEvent) => void,
): number {
  const unit = requireUnit(state, unitId);
  const adjusted = requested < 0 ? Math.round(requested * (1 - unit.morale.resilience)) : Math.round(requested);
  const before = unit.morale.current;
  unit.morale.current = Math.max(0, Math.min(unit.morale.maximum, before + adjusted));
  const applied = unit.morale.current - before;
  if (applied !== 0) emit({ type: 'moraleChanged', unit: unit.id, amount: applied, current: unit.morale.current, reason });
  if (state.rules.moraleEnabled && unit.morale.current <= 0 && state.units.some((candidate) => candidate.id === unit.id)) {
    routeUnit(state, unit.id, emit);
  }
  return applied;
}

/** Damage shock plus nearby allied defeat shock, independent of damage source. */
export function resolveMoraleAfterDamage(
  state: GameState,
  target: Unit,
  damage: number,
  killed: boolean,
  at: { x: number; y: number },
  emit: (event: GameEvent) => void,
  content: ContentCatalog = GlobalContentCatalog,
): boolean {
  if (!state.rules.moraleEnabled) return false;
  if (!killed && state.units.some((unit) => unit.id === target.id)) {
    const maximumHp = content.units.get(target.type).maxHp;
    const loss = Math.max(1, Math.round(damage / maximumHp * target.morale.maximum * state.rules.moraleDamageFactor));
    changeMorale(state, target.id, -loss, 'damage', emit);
  }
  if (killed) {
    const allies = state.units.filter((unit) => unit.owner === target.owner && dist(unit, at) <= state.rules.moraleDefeatShockRadius);
    for (const ally of allies) changeMorale(state, ally.id, -state.rules.moraleAllyDefeatLoss, 'ally-defeated', emit);
  }
  return !killed && !state.units.some((unit) => unit.id === target.id);
}
