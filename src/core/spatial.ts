import type { Coord, Direction, GameState, Unit } from './types';
import { dist } from './grid';
import { areAllies, unitAt } from './state';

export const DIRECTIONS: readonly Direction[] = ['north', 'east', 'south', 'west'];

export const DIRECTION_VECTOR: Readonly<Record<Direction, Coord>> = {
  north: { x: 0, y: -1 },
  east: { x: 1, y: 0 },
  south: { x: 0, y: 1 },
  west: { x: -1, y: 0 },
};

export const oppositeDirection = (direction: Direction): Direction =>
  ({ north: 'south', east: 'west', south: 'north', west: 'east' })[direction] as Direction;

/** Cardinal direction from one coordinate toward another; dominant axis wins. */
export function directionToward(from: Coord, to: Coord): Direction {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  if (Math.abs(dx) > Math.abs(dy)) return dx >= 0 ? 'east' : 'west';
  return dy >= 0 ? 'south' : 'north';
}

export type RelativeAttackSide = 'front' | 'side' | 'back';

export function relativeAttackSide(defender: Unit, attackerAt: Coord): RelativeAttackSide {
  const incoming = directionToward(defender, attackerAt);
  if (incoming === defender.facing) return 'front';
  if (incoming === oppositeDirection(defender.facing)) return 'back';
  return 'side';
}

/** A melee attacker flanks when an ally occupies the exact opposite adjacent cell. */
export function hasOpposedFlanker(state: GameState, attacker: Unit, defender: Unit, attackerAt: Coord): boolean {
  if (dist(attackerAt, defender) !== 1) return false;
  const attackDirection = directionToward(defender, attackerAt);
  const opposite = DIRECTION_VECTOR[oppositeDirection(attackDirection)];
  const ally = unitAt(state, defender.x + opposite.x, defender.y + opposite.y);
  return Boolean(
    ally &&
    ally.id !== attacker.id &&
    areAllies(state, ally.owner, attacker.owner),
  );
}

export function edgeKey(a: Coord, b: Coord): string {
  const ends = [`${a.x},${a.y}`, `${b.x},${b.y}`].sort();
  return ends.join('|');
}
