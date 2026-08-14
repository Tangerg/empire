import { boardOf, type Board } from './domain/board';
import { KeyedRegistry } from './registry';
import type { Coord, GameState, WeaponArea } from './types';
import type { GridRules } from './tactical-grid';

/**
 * The tiles one strike covers, once its aim point is fixed.
 *
 * Area was a closed union of four names and a `switch` that read them, so a
 * dragon's breath cone or a five-by-five siege blast was a change to the core
 * rather than to a content pack — even though the shape of a blast is content
 * in exactly the way a weapon's power is. It is an open registry now, and the
 * two places that asked `area === 'single'` ask the shape instead.
 */
export interface WeaponAreaShape {
  readonly id: WeaponArea;
  /**
   * A strike that covers nothing but its aim point needs something standing
   * there. A blast still falls on an emptied tile and splashes the neighbours,
   * which is the whole point of charge time.
   */
  readonly needsOccupant: boolean;
  /**
   * `from` matters to shapes that extend away from the attacker, like a line.
   *
   * The board comes first because a shape is drawn *on* one: a cross on a hex
   * board is six cells, not four, and the shape does not get to decide that.
   */
  cells(board: Board, from: Coord, aimedAt: Coord): Coord[];
}

export class WeaponAreaShapeRegistry extends KeyedRegistry<WeaponArea, WeaponAreaShape> {
  constructor() {
    super('weapon area shape');
  }

  protected keyOf(shape: WeaponAreaShape): WeaponArea {
    return shape.id;
  }

  /** Deterministic cells affected, each tile once, in shape order. */
  coverage(board: Board, from: Coord, aimedAt: Coord, area: WeaponArea): Coord[] {
    const unique = new Map<number, Coord>();
    for (const cell of this.get(area).cells(board, from, aimedAt)) {
      if (!board.contains(cell)) continue;
      unique.set(board.indexOf(cell), cell);
    }
    return [...unique.values()];
  }

  clone(): WeaponAreaShapeRegistry {
    return this.copyInto(new WeaponAreaShapeRegistry());
  }
}

export const WeaponAreaShapes = new WeaponAreaShapeRegistry()
  .register({
    id: 'single',
    needsOccupant: true,
    cells: (_board, _from, aimedAt) => [{ ...aimedAt }],
  })
  .register({
    id: 'cross1',
    needsOccupant: false,
    // Whatever "next to" means on this board: four cells, eight, or six.
    cells: (board, _from, aimedAt) => board.ring(aimedAt, 0, 1),
  })
  .register({
    id: 'ring1',
    needsOccupant: false,
    // Deliberately the storage square, not the tiling's ring: a nine-cell blast
    // is a shape an author draws on the map, and it stays nine cells everywhere.
    cells: (board, _from, aimedAt) => {
      const cells: Coord[] = [];
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const cell = { x: aimedAt.x + dx, y: aimedAt.y + dy };
          if (board.contains(cell)) cells.push(cell);
        }
      }
      return cells;
    },
  })
  .register({
    id: 'line',
    needsOccupant: false,
    // The attacker's own tile is not in its own blast.
    cells: (board, from, aimedAt) => board.line(from, aimedAt).slice(1),
  });

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface WeaponAreaRules extends GridRules {
  readonly areaShapes: WeaponAreaShapeRegistry;
}

/**
 * The tiles this weapon covers, fired from `from` at `aimedAt`.
 *
 * This module declared the port and then answered nothing with it: combat
 * planning and the AI's threat map each wrote `areaShapes.coverage(boardOf(…))`
 * out for themselves, which is two copies of "a blast is drawn on the board
 * this battle is tiled with" — the half of the rule a shape is not allowed to
 * decide for itself.
 */
export function weaponCoverage(
  rules: WeaponAreaRules,
  state: GameState,
  from: Coord,
  aimedAt: Coord,
  area: WeaponArea,
): Coord[] {
  return rules.areaShapes.coverage(boardOf(rules, state), from, aimedAt, area);
}

/**
 * Does a strike with this shape need something standing on the tile it is
 * aimed at? Asked by the planner and by the charge queue, which had to agree.
 */
export function needsOccupant(rules: WeaponAreaRules, area: WeaponArea): boolean {
  return rules.areaShapes.get(area).needsOccupant;
}
