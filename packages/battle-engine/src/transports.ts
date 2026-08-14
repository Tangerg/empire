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

function transportProfile(state: GameState, carrierId: number, content: ContentCatalog) {
  const carrier = requireUnit(state, carrierId);
  const profile = content.units.get(carrier.type).transport;
  if (!profile) throw new IllegalActionError(`unit ${carrierId} is not a transport`);
  return { carrier, profile };
}

export function passengersOf(state: GameState, carrier: number): Unit[] {
  return state.embarkedUnits.filter((entry) => entry.carrier === carrier).map((entry) => entry.unit);
}

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface TransportRules extends GridRules {
  readonly content: ContentCatalog;
}

export function embarkUnit(
  rules: TransportRules,
  state: GameState,
  unitId: number,
  carrierId: number,
  emit: (event: GameEvent) => void,
): void {
  const content = rules.content;
  if (unitId === carrierId) throw new IllegalActionError('a transport cannot embark itself');
  const unit = requireUnit(state, unitId);
  const { carrier, profile } = transportProfile(state, carrierId, content);
  if (!areAllies(state, unit.owner, carrier.owner)) throw new IllegalActionError('transport and passenger are not allied');
  if (boardOf(rules, state).distance(unit, carrier) !== 1) {
    throw new IllegalActionError('passenger must be adjacent to transport');
  }
  if (passengersOf(state, carrierId).length >= profile.capacity) throw new IllegalActionError('transport is full');
  if (content.units.get(unit.type).transport) throw new IllegalActionError('nested transports are not supported');
  const tags = content.units.get(unit.type).tags;
  if (profile.allowedTags?.length && !profile.allowedTags.some((tag) => tags.includes(tag))) {
    throw new IllegalActionError('passenger type is not allowed by transport');
  }
  if (profile.forbiddenTags?.some((tag) => tags.includes(tag))) {
    throw new IllegalActionError('passenger type is forbidden by transport');
  }
  const snapshot = cloneUnitState(unit);
  const boarding = new UnitEntity(snapshot);
  boarding.finishAction();
  boarding.clearCapture();
  state.embarkedUnits.push({ carrier: carrierId, unit: snapshot });
  removeUnit(state, unitId);
  emit({ type: 'unitEmbarked', unit: unitId, carrier: carrierId });
}

export function disembarkUnit(
  rules: TransportRules,
  state: GameState,
  carrierId: number,
  unitId: number,
  at: Coord,
  emit: (event: GameEvent) => void,
): void {
  const content = rules.content;
  const { carrier } = transportProfile(state, carrierId, content);
  const index = state.embarkedUnits.findIndex((entry) => entry.carrier === carrierId && entry.unit.id === unitId);
  if (index < 0) throw new IllegalActionError(`unit ${unitId} is not aboard transport ${carrierId}`);
  if (!inBounds(state.map, at.x, at.y) || boardOf(rules, state).distance(carrier, at) !== 1) {
    throw new IllegalActionError('disembark cell must be adjacent');
  }
  if (unitAt(state, at)) throw new IllegalActionError('disembark cell is occupied');
  const unit = state.embarkedUnits[index].unit;
  const movement = content.units.get(unit.type).movementClass;
  if (!new Battlefield(state, content).cell(at).admits(movement)) {
    throw new IllegalActionError('passenger cannot enter disembark cell');
  }
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
