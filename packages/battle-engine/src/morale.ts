import { dist } from './grid';
import { areAllies, removeUnit, requireUnit } from './state';
import type { ContentCatalog } from './content-pack';
import type { BattlefieldMarker, GameEvent, GameState, PlayerId, Unit } from './types';
import { withdrawTransportPassengers } from './transports';
import { BattleAggregate } from './domain/battle-aggregate';

function withdrawalMarker(
  state: GameState,
  unit: Unit,
  kind: string,
  meta: BattlefieldMarker['meta'],
  content: ContentCatalog,
): BattlefieldMarker {
  const marker = new BattleAggregate(state, content).createUnitMarker(unit, kind, meta);
  removeUnit(state, unit.id);
  return marker;
}

export function routeUnit(
  content: ContentCatalog,
  state: GameState,
  unitId: number,
  emit: (event: GameEvent) => void,
): BattlefieldMarker {
  const unit = requireUnit(state, unitId);
  const marker = withdrawalMarker(state, unit, 'routed', {}, content);
  withdrawTransportPassengers(state, unitId, marker.at, 'routed', emit);
  emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({ type: 'unitRouted', unit: unitId, marker: marker.id, at: marker.at });
  return marker;
}

export function surrenderUnit(
  content: ContentCatalog,
  state: GameState,
  unitId: number,
  to: PlayerId | undefined,
  emit: (event: GameEvent) => void,
): BattlefieldMarker {
  const unit = requireUnit(state, unitId);
  const marker = withdrawalMarker(state, unit, 'surrendered', to === undefined ? {} : { surrenderedTo: to }, content);
  withdrawTransportPassengers(
    state,
    unitId,
    marker.at,
    'surrendered',
    emit,
    to === undefined ? {} : { surrenderedTo: to },
  );
  emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({ type: 'unitSurrendered', unit: unitId, marker: marker.id, at: marker.at, to });
  return marker;
}

export function changeMorale(
  content: ContentCatalog,
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
    routeUnit(content, state, unit.id, emit);
  }
  return applied;
}

/** Damage shock plus nearby allied defeat shock, independent of damage source. */
export function resolveMoraleAfterDamage(
  content: ContentCatalog,
  state: GameState,
  target: Unit,
  damage: number,
  killed: boolean,
  at: { x: number; y: number },
  emit: (event: GameEvent) => void,
): boolean {
  if (!state.rules.moraleEnabled) return false;
  if (!killed && state.units.some((unit) => unit.id === target.id)) {
    const maximumHp = content.units.get(target.type).maxHp;
    const loss = Math.max(1, Math.round(damage / maximumHp * target.morale.maximum * state.rules.moraleDamageFactor));
    changeMorale(content, state, target.id, -loss, 'damage', emit);
  }
  if (killed) {
    const allies = state.units.filter((unit) =>
      areAllies(state, unit.owner, target.owner) && dist(unit, at) <= state.rules.moraleDefeatShockRadius);
    for (const ally of allies) changeMorale(content, state, ally.id, -state.rules.moraleAllyDefeatLoss, 'ally-defeated', emit);
  }
  return !killed && !state.units.some((unit) => unit.id === target.id);
}
