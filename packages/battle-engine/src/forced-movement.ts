import { Battlefield } from './domain/battlefield';
import { BattleAggregate } from './domain/battle-aggregate';
import { UnitEntity } from './domain/unit-entity';
import { sameCoord } from './grid';
import { resolveDamage, type DamageRules } from './damage';
import { DIRECTION_VECTOR, directionToward } from './spatial';
import { requireUnit } from './state';
import type { Coord, GameEvent, GameState, Unit } from './types';
import { type ContentCatalog } from './content-pack';

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export type ForcedMovementRules = DamageRules;

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

function canOccupy(battlefield: Battlefield, unit: Unit, at: Coord): boolean {
  return battlefield.contains(at) &&
    battlefield.cell(at).canReceive(battlefield.content.units.get(unit.type).movementClass);
}

/**
 * Authoritative straight-line displacement used by weapons, abilities and the
 * scenario DSL. It ignores movement points but still respects occupancy,
 * impassable terrain, elevation limits and cliffs.
 */
export function forceMoveUnit(
  rules: ForcedMovementRules,
  state: GameState,
  request: ForcedMovementRequest,
  emit: (event: GameEvent) => void,
): ForcedMovementResult {
  const content = rules.content;
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
    // Nothing to be shoved into, and an edge the shove can cross.
    if (!canOccupy(battlefield, unit, next) ||
      battlefield.traversalCost(current, next, content.units.get(unit.type).movementClass) === null) {
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

  const collisionDamage = Math.max(0, Math.round(request.collisionDamage ?? 0));
  const impact = collided && collisionDamage > 0
    ? resolveDamage(rules, state, {
      unit: unit.id,
      amount: collisionDamage,
      report: (blow) => ({
        type: 'collisionDamage',
        unit: unit.id,
        amount: blow.amount,
        hpAfter: blow.hpAfter,
        killed: blow.killed,
      }),
    }, emit)
    : null;
  return { from, to: { ...to }, path, collided, killed: impact?.killed ?? false };
}

/** Teleportation ignores intermediate edges, but never creates an invalid overlap. */
export function teleportUnit(
  content: ContentCatalog,
  state: GameState,
  unitId: number,
  destination: Coord,
  emit: (event: GameEvent) => void,
): boolean {
  const unit = requireUnit(state, unitId);
  const from = { x: unit.x, y: unit.y };
  if (sameCoord(from, destination)) return true;
  if (!canOccupy(new Battlefield(state, content), unit, destination)) return false;
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
