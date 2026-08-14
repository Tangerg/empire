import { boardOf } from './domain/board';
import { areAllies, removeUnit, requireUnit } from './state';
import type { ContentCatalog } from './content-pack';
import type { BattlefieldMarker, Coord, GameEvent, GameState, PlayerId, Unit } from './types';
import { withdrawTransportPassengers } from './transports';
import { BattleAggregate } from './domain/battle-aggregate';
import { UnitEntity } from './domain/unit-entity';
import { announceUnitDeparture, type UnitDepartureRules } from './unit-departure';

/**
 * Port declared by this module; `BattleRuleServices` satisfies it.
 *
 * Morale needs the departure channel because breaking is a way of leaving the
 * field: a routed commander's aura must collapse exactly as a slain one's does.
 */
export type MoraleRules = UnitDepartureRules;

function withdrawalMarker(
  content: ContentCatalog,
  state: GameState,
  unit: Unit,
  kind: string,
  meta: BattlefieldMarker['meta'],
): BattlefieldMarker {
  const marker = new BattleAggregate(state, content).createUnitMarker(unit, kind, meta);
  removeUnit(state, unit.id);
  return marker;
}

export function routeUnit(
  rules: MoraleRules,
  state: GameState,
  unitId: number,
  emit: (event: GameEvent) => void,
): BattlefieldMarker {
  const unit = requireUnit(state, unitId);
  const marker = withdrawalMarker(rules.content, state, unit, 'routed', {});
  withdrawTransportPassengers(state, unitId, marker.at, 'routed', emit);
  emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({ type: 'unitRouted', unit: unitId, marker: marker.id, at: marker.at });
  announceUnitDeparture(rules, state, unit, emit);
  return marker;
}

export function surrenderUnit(
  rules: MoraleRules,
  state: GameState,
  unitId: number,
  to: PlayerId | undefined,
  emit: (event: GameEvent) => void,
): BattlefieldMarker {
  const unit = requireUnit(state, unitId);
  const meta: BattlefieldMarker['meta'] = to === undefined ? {} : { surrenderedTo: to };
  const marker = withdrawalMarker(rules.content, state, unit, 'surrendered', meta);
  withdrawTransportPassengers(state, unitId, marker.at, 'surrendered', emit, meta);
  emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({ type: 'unitSurrendered', unit: unitId, marker: marker.id, at: marker.at, to });
  announceUnitDeparture(rules, state, unit, emit);
  return marker;
}

export function changeMorale(
  rules: MoraleRules,
  state: GameState,
  unitId: number,
  requested: number,
  reason: string,
  emit: (event: GameEvent) => void,
): number {
  const unit = requireUnit(state, unitId);
  const adjusted = requested < 0 ? Math.round(requested * (1 - unit.morale.resilience)) : Math.round(requested);
  const applied = new UnitEntity(unit).changeMorale(adjusted);
  if (applied !== 0) emit({ type: 'moraleChanged', unit: unit.id, amount: applied, current: unit.morale.current, reason });
  if (state.rules.moraleEnabled && unit.morale.current <= 0 && state.units.some((candidate) => candidate.id === unit.id)) {
    routeUnit(rules, state, unit.id, emit);
  }
  return applied;
}

/**
 * Morale lost for having been hit. May break the unit, which routs it off the
 * field — the rout announces its own departure, so no caller has to notice.
 */
export function sufferDamageShock(
  rules: MoraleRules,
  state: GameState,
  target: Unit,
  damage: number,
  emit: (event: GameEvent) => void,
): void {
  if (!state.rules.moraleEnabled) return;
  if (!state.units.some((unit) => unit.id === target.id)) return;
  const maximumHp = rules.content.units.get(target.type).maxHp;
  const loss = Math.max(1, Math.round(damage / maximumHp * target.morale.maximum * state.rules.moraleDamageFactor));
  changeMorale(rules, state, target.id, -loss, 'damage', emit);
}

/** Morale lost by everyone close enough to watch an ally go down. */
export function mournFallen(
  rules: MoraleRules,
  state: GameState,
  fallen: Unit,
  at: Coord,
  emit: (event: GameEvent) => void,
): void {
  if (!state.rules.moraleEnabled) return;
  const mourners = state.units.filter((unit) =>
    areAllies(state, unit.owner, fallen.owner) &&
    boardOf(rules, state).distance(unit, at) <= state.rules.moraleDefeatShockRadius);
  for (const mourner of mourners) {
    changeMorale(rules, state, mourner.id, -state.rules.moraleAllyDefeatLoss, 'ally-defeated', emit);
  }
}
