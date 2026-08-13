import { Battlefield } from './domain/battlefield';
import { BattleAggregate } from './domain/battle-aggregate';
import { UnitEntity } from './domain/unit-entity';
import { inBounds, sameCoord } from './grid';
import { handleCommanderDefeat } from './commanders';
import { DIRECTION_VECTOR, directionToward } from './spatial';
import { requireUnit, unitAtCoord } from './state';
import type { Coord, GameEvent, GameState, Unit } from './types';
import { type ContentCatalog } from './content-pack';
import { resolveMoraleAfterDamage } from './morale';
import { emitTransportLossEvents } from './transports';

export type ForcedMovementMode = 'push' | 'pull';

export interface ForcedMovementRequest {
  unit: number;
  source: Coord;
  mode: ForcedMovementMode;
  distance: number;
  collisionDamage?: number;
}

export interface ForcedMovementResult {
  from: Coord;
  to: Coord;
  path: Coord[];
  collided: boolean;
  killed: boolean;
}

function canOccupy(state: GameState, unit: Unit, at: Coord, content: ContentCatalog): boolean {
  if (!inBounds(state.map, at.x, at.y)) return false;
  if (unitAtCoord(state, at)) return false;
  const battlefield = new Battlefield(state, content);
  const cell = battlefield.cell(at);
  return !cell.blocksMovement && cell.movementCost(content.units.get(unit.type).movementClass) !== null;
}

/**
 * Authoritative straight-line displacement used by weapons, abilities and the
 * scenario DSL. It ignores movement points but still respects occupancy,
 * impassable terrain, elevation limits and cliffs.
 */
export function forceMoveUnit(
  state: GameState,
  request: ForcedMovementRequest,
  emit: (event: GameEvent) => void,
  content: ContentCatalog,
): ForcedMovementResult {
  const unit = requireUnit(state, request.unit);
  const from = { x: unit.x, y: unit.y };
  const distance = Math.max(0, Math.round(request.distance));
  const facing = request.mode === 'push'
    ? directionToward(request.source, from)
    : directionToward(from, request.source);
  const vector = DIRECTION_VECTOR[facing];
  const path: Coord[] = [{ ...from }];
  const battlefield = new Battlefield(state, content);
  let collided = false;

  if (sameCoord(from, request.source) && distance > 0) collided = true;
  for (let step = 0; step < distance && !collided; step++) {
    const current = path[path.length - 1];
    const next = { x: current.x + vector.x, y: current.y + vector.y };
    if (!canOccupy(state, unit, next, content)) {
      collided = true;
      break;
    }
    if (battlefield.traversalCost(current, next, content.units.get(unit.type).movementClass) === null) {
      collided = true;
      break;
    }
    path.push(next);
  }

  const to = path[path.length - 1];
  if (!sameCoord(from, to)) {
    new BattleAggregate(state, content).moveUnit(unit.id, to);
    new UnitEntity(unit).changeFacing(facing);
  }
  emit({ type: 'forcedMove', unit: unit.id, mode: request.mode, from, to: { ...to }, path, collided });

  let killed = false;
  const collisionDamage = Math.max(0, Math.round(request.collisionDamage ?? 0));
  if (collided && collisionDamage > 0 && state.units.some((candidate) => candidate.id === unit.id)) {
    const result = new BattleAggregate(state, content).damageUnit(unit.id, collisionDamage);
    killed = result.killed;
    emit({ type: 'collisionDamage', unit: unit.id, amount: result.amount, hpAfter: result.hpAfter, killed });
    if (killed) {
      emit({ type: 'death', unit: unit.id, at: result.at });
      if (result.marker) emit({ type: 'markerAdded', marker: result.marker.id, kind: result.marker.kind, at: result.marker.at });
      handleCommanderDefeat(state, unit.id, emit, content);
      emitTransportLossEvents(unit.id, result.at, result.passengerMarkers, emit);
      resolveMoraleAfterDamage(state, unit, result.amount, true, result.at, emit, content);
    } else if (resolveMoraleAfterDamage(state, unit, result.amount, false, result.at, emit, content)) {
      handleCommanderDefeat(state, unit.id, emit, content);
    }
  }
  return { from, to: { ...to }, path, collided, killed };
}

/** Teleportation ignores intermediate edges, but never creates an invalid overlap. */
export function teleportUnit(
  state: GameState,
  unitId: number,
  destination: Coord,
  emit: (event: GameEvent) => void,
  content: ContentCatalog,
): boolean {
  const unit = requireUnit(state, unitId);
  const from = { x: unit.x, y: unit.y };
  if (sameCoord(from, destination)) return true;
  if (!canOccupy(state, unit, destination, content)) return false;
  new BattleAggregate(state, content).moveUnit(unit.id, destination);
  emit({
    type: 'forcedMove',
    unit: unit.id,
    mode: 'teleport',
    from,
    to: { ...destination },
    path: [from, { ...destination }],
    collided: false,
  });
  return true;
}
