import { idx, inBounds, lineBetween, ring } from './grid';
import { KeyedRegistry } from './registry';
import type { Coord, GameMap, WeaponArea } from './types';

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
  /** `from` matters to shapes that extend away from the attacker, like a line. */
  cells(map: GameMap, from: Coord, aimedAt: Coord): Coord[];
}

export class WeaponAreaShapeRegistry extends KeyedRegistry<WeaponArea, WeaponAreaShape> {
  constructor() {
    super('weapon area shape');
  }

  protected keyOf(shape: WeaponAreaShape): WeaponArea {
    return shape.id;
  }

  /** Deterministic cells affected, each tile once, in shape order. */
  coverage(map: GameMap, from: Coord, aimedAt: Coord, area: WeaponArea): Coord[] {
    const unique = new Map<number, Coord>();
    for (const cell of this.get(area).cells(map, from, aimedAt)) {
      unique.set(idx(map, cell.x, cell.y), cell);
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
    cells: (_map, _from, aimedAt) => [{ ...aimedAt }],
  })
  .register({
    id: 'cross1',
    needsOccupant: false,
    cells: (map, _from, aimedAt) => ring(map, aimedAt, 0, 1),
  })
  .register({
    id: 'ring1',
    needsOccupant: false,
    cells: (map, _from, aimedAt) => {
      const cells: Coord[] = [];
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const x = aimedAt.x + dx;
          const y = aimedAt.y + dy;
          if (inBounds(map, x, y)) cells.push({ x, y });
        }
      }
      return cells;
    },
  })
  .register({
    id: 'line',
    needsOccupant: false,
    // The attacker's own tile is not in its own blast.
    cells: (_map, from, aimedAt) => lineBetween(from, aimedAt).slice(1),
  });

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface WeaponAreaRules {
  readonly areaShapes: WeaponAreaShapeRegistry;
}
