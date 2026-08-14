import type { Coord, GameState, Unit } from './types';
export { edgeKey } from './grid';
import { areAllies, unitAt } from './state';
import type { Board } from './domain/board';
import type { TacticalGrid } from './tactical-grid';

/**
 * Which face of a unit an attack lands on.
 *
 * The direction set, the opposite of a direction and which way one cell lies
 * from another all moved to the tiling: this module used to hold four names,
 * four vectors and a dominant-axis rule, which is a four-directional board
 * written into the flanking rules.
 */
export type RelativeAttackSide = 'front' | 'side' | 'back';

export function relativeAttackSide(
  grid: TacticalGrid,
  defender: Unit,
  attackerAt: Coord,
): RelativeAttackSide {
  const incoming = grid.toward(defender, attackerAt);
  if (incoming === defender.facing) return 'front';
  if (incoming === grid.opposite(defender.facing)) return 'back';
  return 'side';
}

/** A melee attacker flanks when an ally occupies the exact opposite adjacent cell. */
export function hasOpposedFlanker(
  board: Board,
  state: GameState,
  attacker: Unit,
  defender: Unit,
  attackerAt: Coord,
): boolean {
  if (board.distance(attackerAt, defender) !== 1) return false;
  const behind = board.grid.step(defender, board.grid.opposite(board.grid.toward(defender, attackerAt)));
  const ally = unitAt(state, { x: behind.x, y: behind.y });
  return Boolean(
    ally &&
    ally.id !== attacker.id &&
    areAllies(state, ally.owner, attacker.owner),
  );
}

