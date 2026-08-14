import { IllegalActionError } from './domain/errors';
import { UnitEntity } from './domain/unit-entity';
import { Battlefield } from './domain/battlefield';
import { boardOf } from './domain/board';
import type { GridRules } from './tactical-grid';
import { inBounds } from './grid';
import { areAllies, removeUnit, requireUnit, unitAt } from './state';
import type { ContentCatalog } from './content-pack';
import type { BattlefieldMarker, Coord, GameEvent, GameState, Unit } from './types';
import { cloneUnitState } from './unit-state';

export function passengersOf(state: GameState, carrier: number): Unit[] {
  return state.embarkedUnits.filter((entry) => entry.carrier === carrier).map((entry) => entry.unit);
}

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface TransportRules extends GridRules {
  readonly content: ContentCatalog;
}

/** One carrier a unit could board, and whether it may right now. */
export interface CarrierOption {
  carrier: Unit;
  /** Boarding would be accepted; otherwise `reasons` says what stops it. */
  eligible: boolean;
  reasons: string[];
}

/** One passenger aboard a carrier, and where it may step off. */
export interface PassengerOption {
  unit: Unit;
  /** Adjacent cells this passenger may land on; empty means it is stuck. */
  spots: Coord[];
}

/**
 * Why this unit may not board this carrier, or `null` when it may.
 *
 * The one place the rule lives. `embarkUnit` throws what it answers and
 * `carrierOptions` reports it beside a disabled entry — the same arrangement
 * deployment uses, and for the same reason: a rule that only exists in its
 * committing form cannot be offered by any interface, which is how transports
 * shipped with rules, events, save coverage and nothing able to reach them.
 */
export function embarkRefusal(
  rules: TransportRules,
  state: GameState,
  unit: Unit,
  carrier: Unit,
): string | null {
  const content = rules.content;
  if (unit.id === carrier.id) return '载具不能登载自己';
  const profile = content.units.get(carrier.type).transport;
  if (!profile) return `${content.units.get(carrier.type).name} 不是载具`;
  if (!areAllies(state, unit.owner, carrier.owner)) return '载具与乘员不是友军';
  if (boardOf(rules, state).distance(unit, carrier) !== 1) return '乘员必须与载具相邻';
  if (passengersOf(state, carrier.id).length >= profile.capacity) return '载具已满';
  if (content.units.get(unit.type).transport) return '载具不能登载载具';
  const tags = content.units.get(unit.type).tags;
  if (profile.allowedTags?.length && !profile.allowedTags.some((tag) => tags.includes(tag))) {
    return '该载具不接受这个兵种';
  }
  if (profile.forbiddenTags?.some((tag) => tags.includes(tag))) return '该载具拒载这个兵种';
  return null;
}

/** The carriers standing beside this unit, in board order, eligible or not. */
export function carrierOptions(
  rules: TransportRules,
  state: GameState,
  unit: Unit,
): CarrierOption[] {
  const board = boardOf(rules, state);
  return state.units.flatMap((candidate) => {
    if (candidate.id === unit.id) return [];
    if (!rules.content.units.get(candidate.type).transport) return [];
    if (!areAllies(state, unit.owner, candidate.owner)) return [];
    if (board.distance(unit, candidate) !== 1) return [];
    const refusal = embarkRefusal(rules, state, unit, candidate);
    return [{ carrier: candidate, eligible: refusal === null, reasons: refusal ? [refusal] : [] }];
  });
}

export function embarkUnit(
  rules: TransportRules,
  state: GameState,
  unitId: number,
  carrierId: number,
  emit: (event: GameEvent) => void,
): void {
  const unit = requireUnit(state, unitId);
  const carrier = requireUnit(state, carrierId);
  const refusal = embarkRefusal(rules, state, unit, carrier);
  if (refusal) throw new IllegalActionError(refusal);
  const snapshot = cloneUnitState(unit);
  const boarding = new UnitEntity(snapshot);
  boarding.finishAction();
  boarding.clearCapture();
  state.embarkedUnits.push({ carrier: carrierId, unit: snapshot });
  removeUnit(state, unitId);
  emit({ type: 'unitEmbarked', unit: unitId, carrier: carrierId });
}

/** Why this passenger may not step off here, or `null` when it may. */
export function disembarkRefusal(
  rules: TransportRules,
  state: GameState,
  carrierId: number,
  unitId: number,
  at: Coord,
): string | null {
  const content = rules.content;
  const carrier = state.units.find((candidate) => candidate.id === carrierId);
  if (!carrier) return `载具 ${carrierId} 不在场上`;
  if (!content.units.get(carrier.type).transport) return `${content.units.get(carrier.type).name} 不是载具`;
  const entry = state.embarkedUnits.find((candidate) =>
    candidate.carrier === carrierId && candidate.unit.id === unitId);
  if (!entry) return '该单位不在这辆载具上';
  if (!inBounds(state.map, at.x, at.y) || boardOf(rules, state).distance(carrier, at) !== 1) {
    return '卸载格必须与载具相邻';
  }
  if (unitAt(state, at)) return '卸载格已被占据';
  if (!new Battlefield(state, content).cell(at).admits(content.units.get(entry.unit.type).movementClass)) {
    return '该乘员无法进入卸载格';
  }
  return null;
}

/** Who is aboard this carrier, and where each of them may step off. */
export function passengerOptions(
  rules: TransportRules,
  state: GameState,
  carrier: Unit,
): PassengerOption[] {
  const board = boardOf(rules, state);
  return passengersOf(state, carrier.id).map((unit) => ({
    unit,
    spots: board.neighbours(carrier)
      .filter((at) => disembarkRefusal(rules, state, carrier.id, unit.id, at) === null),
  }));
}

export function disembarkUnit(
  rules: TransportRules,
  state: GameState,
  carrierId: number,
  unitId: number,
  at: Coord,
  emit: (event: GameEvent) => void,
): void {
  const refusal = disembarkRefusal(rules, state, carrierId, unitId, at);
  if (refusal) throw new IllegalActionError(refusal);
  const index = state.embarkedUnits.findIndex((entry) => entry.carrier === carrierId && entry.unit.id === unitId);
  const unit = state.embarkedUnits[index].unit;
  const landing = new UnitEntity(unit);
  landing.moveTo(at);
  landing.finishAction();
  state.embarkedUnits.splice(index, 1);
  state.units.push(unit);
  emit({ type: 'unitDisembarked', unit: unitId, carrier: carrierId, at: { ...at } });
}

/** State-only half of fatal transport cleanup, owned by the battle aggregate. */
export function extractLostTransportPassengers(
  state: GameState,
  carrierId: number,
  at: Coord,
): BattlefieldMarker[] {
  const markers: BattlefieldMarker[] = [];
  for (const entry of state.embarkedUnits.filter((candidate) => candidate.carrier === carrierId)) {
    const marker: BattlefieldMarker = {
      id: state.nextMarkerId++,
      kind: 'transport-loss',
      at: { ...at },
      owner: entry.unit.owner,
      fallenUnit: cloneUnitState(entry.unit),
      meta: { carrier: carrierId },
    };
    state.markers.push(marker);
    markers.push(marker);
  }
  state.embarkedUnits = state.embarkedUnits.filter((entry) => entry.carrier !== carrierId);
  return markers;
}

export function emitTransportLossEvents(
  carrierId: number,
  at: Coord,
  markers: readonly BattlefieldMarker[],
  emit: (event: GameEvent) => void,
): void {
  if (markers.length === 0) return;
  for (const marker of markers) emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({
    type: 'transportLost',
    carrier: carrierId,
    passengers: markers.flatMap((marker) => marker.fallenUnit ? [marker.fallenUnit.id] : []),
    at: { ...at },
  });
}

/** Removes passengers together with a non-fatal carrier withdrawal. */
export function withdrawTransportPassengers(
  state: GameState,
  carrierId: number,
  at: Coord,
  kind: 'routed' | 'surrendered' | 'withdrawn',
  emit: (event: GameEvent) => void,
  meta: Record<string, number | string | boolean> = {},
): number[] {
  const entries = state.embarkedUnits.filter((entry) => entry.carrier === carrierId);
  const ids: number[] = [];
  for (const entry of entries) {
    ids.push(entry.unit.id);
    const marker = {
      id: state.nextMarkerId++,
      kind,
      at: { ...at },
      owner: entry.unit.owner,
      fallenUnit: cloneUnitState(entry.unit),
      meta: { carrier: carrierId, ...meta },
    };
    state.markers.push(marker);
    emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
  }
  state.embarkedUnits = state.embarkedUnits.filter((entry) => entry.carrier !== carrierId);
  return ids;
}
