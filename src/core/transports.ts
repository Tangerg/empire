import { Battlefield } from './domain/battlefield';
import { dist, inBounds } from './grid';
import { areAllies, removeUnit, requireUnit, unitAtCoord } from './state';
import type { ContentCatalog } from './content-pack';
import { GlobalContentCatalog } from './content-pack';
import type { BattlefieldMarker, Coord, GameEvent, GameState, Unit } from './types';
import { cloneUnitState } from './unit-state';

function transportProfile(state: GameState, carrierId: number, content: ContentCatalog) {
  const carrier = requireUnit(state, carrierId);
  const profile = content.units.get(carrier.type).transport;
  if (!profile) throw new Error(`unit ${carrierId} is not a transport`);
  return { carrier, profile };
}

export function passengersOf(state: GameState, carrier: number): Unit[] {
  return state.embarkedUnits.filter((entry) => entry.carrier === carrier).map((entry) => entry.unit);
}

export function embarkUnit(
  state: GameState,
  unitId: number,
  carrierId: number,
  emit: (event: GameEvent) => void,
  content: ContentCatalog = GlobalContentCatalog,
): void {
  if (unitId === carrierId) throw new Error('a transport cannot embark itself');
  const unit = requireUnit(state, unitId);
  const { carrier, profile } = transportProfile(state, carrierId, content);
  if (!areAllies(state, unit.owner, carrier.owner)) throw new Error('transport and passenger are not allied');
  if (dist(unit, carrier) !== 1) throw new Error('passenger must be adjacent to transport');
  if (passengersOf(state, carrierId).length >= profile.capacity) throw new Error('transport is full');
  if (content.units.get(unit.type).transport) throw new Error('nested transports are not supported');
  const tags = content.units.get(unit.type).tags;
  if (profile.allowedTags?.length && !profile.allowedTags.some((tag) => tags.includes(tag))) {
    throw new Error('passenger type is not allowed by transport');
  }
  if (profile.forbiddenTags?.some((tag) => tags.includes(tag))) {
    throw new Error('passenger type is forbidden by transport');
  }
  const snapshot = cloneUnitState(unit);
  snapshot.done = true;
  snapshot.capture = 0;
  state.embarkedUnits.push({ carrier: carrierId, unit: snapshot });
  removeUnit(state, unitId);
  emit({ type: 'unitEmbarked', unit: unitId, carrier: carrierId });
}

export function disembarkUnit(
  state: GameState,
  carrierId: number,
  unitId: number,
  at: Coord,
  emit: (event: GameEvent) => void,
  content: ContentCatalog = GlobalContentCatalog,
): void {
  const { carrier } = transportProfile(state, carrierId, content);
  const index = state.embarkedUnits.findIndex((entry) => entry.carrier === carrierId && entry.unit.id === unitId);
  if (index < 0) throw new Error(`unit ${unitId} is not aboard transport ${carrierId}`);
  if (!inBounds(state.map, at.x, at.y) || dist(carrier, at) !== 1) throw new Error('disembark cell must be adjacent');
  if (unitAtCoord(state, at)) throw new Error('disembark cell is occupied');
  const unit = state.embarkedUnits[index].unit;
  const cell = new Battlefield(state, content).cell(at);
  const movement = content.units.get(unit.type).movementClass;
  if (cell.blocksMovement || cell.movementCost(movement) === null) throw new Error('passenger cannot enter disembark cell');
  unit.x = at.x;
  unit.y = at.y;
  unit.done = true;
  unit.capture = 0;
  state.embarkedUnits.splice(index, 1);
  state.units.push(unit);
  emit({ type: 'unitDisembarked', unit: unitId, carrier: carrierId, at: { ...at } });
}

/** Converts all passengers into persistent casualty markers when a carrier is lost. */
export function loseTransportPassengers(
  state: GameState,
  carrierId: number,
  at: Coord,
  emit: (event: GameEvent) => void,
): number[] {
  const markers = extractLostTransportPassengers(state, carrierId, at);
  emitTransportLossEvents(carrierId, at, markers, emit);
  return markers.map((marker) => marker.fallenUnit!.id);
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
