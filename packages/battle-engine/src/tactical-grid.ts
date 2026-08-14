import { lineBetween } from './grid';
import { ContentRegistry } from './registry';
import type { Coord, Direction, GameState } from './types';

/** A point in tile units — the board's own coordinate system, not pixels. */
export interface TilePoint {
  readonly x: number;
  readonly y: number;
}

/**
 * A direction the tiling admits, and what to call it.
 *
 * The name travels with the direction because the set is no longer fixed: a
 * four-way board faces north/east/south/west, an eight-way board also faces the
 * corners, and a hex board has no north at all. Any menu that offers facing
 * reads this list instead of holding its own copy of four labels.
 */
export interface DirectionDef {
  readonly id: Direction;
  readonly name: string;
}

/** Extent of a board, which is all the tiling needs to know about one. */
export interface GridExtent {
  readonly width: number;
  readonly height: number;
}

/**
 * How a battlefield is tiled: what "one step" means, what "two apart" means, and
 * which way a unit can face.
 *
 * This was hard-coded four-directional geometry spread over a dozen modules —
 * `dist` was Manhattan in fifteen files, `NEIGHBOURS` was four vectors in the
 * pathfinder, `ring` was a Manhattan diamond in five callers, and `Direction`
 * was a closed union of four names. None of it was wrong; all of it was
 * *assumed*, which is why an eight-way or hex board was not a content decision
 * but an engine rewrite.
 *
 * A tiling answers both halves of the question, deliberately: the rules ask how
 * far apart two cells are, and the presentation asks where a cell sits and what
 * shape it is. Both are consequences of the same tiling, and answering them in
 * two places is how a hex board ends up with a square grid drawn over it.
 */
export interface TacticalGrid {
  readonly id: string;
  readonly name: string;
  /** Facings this tiling admits, in presentation order. */
  readonly directions: readonly DirectionDef[];
  /** Steps between two cells. Symmetric, and 1 exactly for neighbours. */
  distance(a: Coord, b: Coord): number;
  /** One step from `at`; may land outside the board, which callers clip. */
  step(at: Coord, direction: Direction): Coord;
  opposite(direction: Direction): Direction;
  /** Which way `to` lies, seen from `from`. */
  toward(from: Coord, to: Coord): Direction;
  /** Cells one step away, unclipped. */
  adjacent(at: Coord): Coord[];
  /** Cells whose distance from `at` is within `[min, max]`, unclipped. */
  within(at: Coord, min: number, max: number): Coord[];
  /**
   * Cells a sight ray passes through, both endpoints included.
   *
   * A ray, not a walk: on a four-way board it moves diagonally, so a wall on the
   * diagonal does not block a diagonal shot. It never skips a storage cell, so
   * everything between the ends is examined.
   */
  line(from: Coord, to: Coord): Coord[];
  /** Centre of a cell in tile units. */
  center(at: Coord): TilePoint;
  /** The cell a point in tile units falls in — the inverse of `center`. */
  cellAt(point: TilePoint): Coord;
  /** Outline of one cell in tile units, relative to its centre. */
  outline(): readonly TilePoint[];
  /** Size of a whole board in tile units, including any row stagger. */
  extent(size: GridExtent): TilePoint;
}

/* ------------------------------------------------------------------- squares */

const SQUARE_DIRECTIONS: readonly DirectionDef[] = [
  { id: 'north', name: '北' },
  { id: 'east', name: '东' },
  { id: 'south', name: '南' },
  { id: 'west', name: '西' },
];

const DIAGONAL_DIRECTIONS: readonly DirectionDef[] = [
  { id: 'northeast', name: '东北' },
  { id: 'southeast', name: '东南' },
  { id: 'southwest', name: '西南' },
  { id: 'northwest', name: '西北' },
];

const SQUARE_VECTORS: Readonly<Record<string, Coord>> = {
  north: { x: 0, y: -1 },
  east: { x: 1, y: 0 },
  south: { x: 0, y: 1 },
  west: { x: -1, y: 0 },
  northeast: { x: 1, y: -1 },
  southeast: { x: 1, y: 1 },
  southwest: { x: -1, y: 1 },
  northwest: { x: -1, y: -1 },
};

const OPPOSITE: Readonly<Record<string, Direction>> = {
  north: 'south', south: 'north', east: 'west', west: 'east',
  northeast: 'southwest', southwest: 'northeast',
  southeast: 'northwest', northwest: 'southeast',
  hexEast: 'hexWest', hexWest: 'hexEast',
  hexNortheast: 'hexSouthwest', hexSouthwest: 'hexNortheast',
  hexNorthwest: 'hexSoutheast', hexSoutheast: 'hexNorthwest',
};

/** Square cells, whether four-way or eight-way: the tiling and art are shared. */
abstract class SquareTiling implements TacticalGrid {
  abstract readonly id: string;
  abstract readonly name: string;
  abstract readonly directions: readonly DirectionDef[];
  abstract distance(a: Coord, b: Coord): number;


  step(at: Coord, direction: Direction): Coord {
    const vector = SQUARE_VECTORS[direction];
    if (!vector) throw new RangeError(`grid "${this.id}" has no direction "${direction}"`);
    return { x: at.x + vector.x, y: at.y + vector.y };
  }

  opposite(direction: Direction): Direction {
    return OPPOSITE[direction] ?? direction;
  }

  adjacent(at: Coord): Coord[] {
    return this.directions.map((direction) => this.step(at, direction.id));
  }

  within(at: Coord, min: number, max: number): Coord[] {
    const out: Coord[] = [];
    for (let dy = -max; dy <= max; dy++) {
      for (let dx = -max; dx <= max; dx++) {
        const cell = { x: at.x + dx, y: at.y + dy };
        const steps = this.distance(at, cell);
        if (steps >= min && steps <= max) out.push(cell);
      }
    }
    return out;
  }

  /**
   * Dominant axis, and a vertical tie goes to the vertical.
   *
   * Preserved to the letter: facing, flanking and which side of a wall an attack
   * comes from all read this, so a four-way board must answer exactly as it did
   * before tilings were a choice.
   */
  toward(from: Coord, to: Coord): Direction {
    const dx = to.x - from.x;
    const dy = to.y - from.y;
    if (Math.abs(dx) > Math.abs(dy)) return dx >= 0 ? 'east' : 'west';
    return dy >= 0 ? 'south' : 'north';
  }

  line(from: Coord, to: Coord): Coord[] {
    return lineBetween(from, to);
  }

  center(at: Coord): TilePoint {
    return { x: at.x + 0.5, y: at.y + 0.5 };
  }

  cellAt(point: TilePoint): Coord {
    return { x: Math.floor(point.x), y: Math.floor(point.y) };
  }

  outline(): readonly TilePoint[] {
    return [{ x: -0.5, y: -0.5 }, { x: 0.5, y: -0.5 }, { x: 0.5, y: 0.5 }, { x: -0.5, y: 0.5 }];
  }

  extent(size: GridExtent): TilePoint {
    return { x: size.width, y: size.height };
  }
}

/**
 * Four-way squares: the classic tactics board, and the default.
 *
 * Manhattan distance, orthogonal steps, four facings. Every shipped level plays
 * on this, and it is deliberately the behaviour the engine had before tilings
 * were a choice at all.
 */
class OrthogonalSquareGrid extends SquareTiling {
  readonly id = 'square4';
  readonly name = '四方格';
  readonly directions = SQUARE_DIRECTIONS;

  distance(a: Coord, b: Coord): number {
    return Math.abs(a.x - b.x) + Math.abs(a.y - b.y);
  }

  /** Kept in declaration order so a pathfinder expands north, east, south, west. */
  override adjacent(at: Coord): Coord[] {
    return [
      { x: at.x, y: at.y - 1 },
      { x: at.x + 1, y: at.y },
      { x: at.x, y: at.y + 1 },
      { x: at.x - 1, y: at.y },
    ];
  }
}

/**
 * Eight-way squares: diagonals count as one step.
 *
 * Chebyshev distance, so a range of three is a 7×7 square rather than a diamond.
 * The point is map design: on a four-way board a diagonal corridor costs twice
 * what it looks like it should, and a defender in a corner cannot be flanked at
 * all. The tiling and the art are identical to `square4` — only what counts as
 * next to what changes — so the whole presentation and editor work unchanged.
 */
class OctileSquareGrid extends SquareTiling {
  readonly id = 'square8';
  readonly name = '八方格';
  readonly directions = [...SQUARE_DIRECTIONS, ...DIAGONAL_DIRECTIONS];

  distance(a: Coord, b: Coord): number {
    return Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y));
  }

  /** Eight sectors of forty-five degrees, so a corner is a facing of its own. */
  override toward(from: Coord, to: Coord): Direction {
    const dx = to.x - from.x;
    const dy = to.y - from.y;
    if (dx === 0 && dy === 0) return 'south';
    const sector = ((Math.round(Math.atan2(dy, dx) / (Math.PI / 4)) % 8) + 8) % 8;
    return OCTILE_SECTORS[sector];
  }
}

/** Sector 0 points east and they turn with the screen, so y grows southward. */
const OCTILE_SECTORS: readonly Direction[] = [
  'east', 'southeast', 'south', 'southwest', 'west', 'northwest', 'north', 'northeast',
];

/* ---------------------------------------------------------------------- hexes */

const HEX_DIRECTIONS: readonly DirectionDef[] = [
  { id: 'hexNortheast', name: '东北' },
  { id: 'hexEast', name: '东' },
  { id: 'hexSoutheast', name: '东南' },
  { id: 'hexSouthwest', name: '西南' },
  { id: 'hexWest', name: '西' },
  { id: 'hexNorthwest', name: '西北' },
];

/** Cube coordinates, where hex distance is simply the largest axis difference. */
interface Cube {
  readonly q: number;
  readonly r: number;
  readonly s: number;
}

const toCube = (at: Coord): Cube => {
  // odd-r offset: odd rows are shifted half a cell to the right.
  const q = at.x - (at.y - (at.y & 1)) / 2;
  const r = at.y;
  return { q, r, s: -q - r };
};

const fromCube = (cube: Cube): Coord => ({
  x: cube.q + (cube.r - (cube.r & 1)) / 2,
  y: cube.r,
});

function roundCube(q: number, r: number, s: number): Cube {
  let rq = Math.round(q);
  let rr = Math.round(r);
  let rs = Math.round(s);
  const dq = Math.abs(rq - q);
  const dr = Math.abs(rr - r);
  const ds = Math.abs(rs - s);
  if (dq > dr && dq > ds) rq = -rr - rs;
  else if (dr > ds) rr = -rq - rs;
  else rs = -rq - rr;
  return { q: rq, r: rr, s: rs };
}

/** Vertical spacing of pointy-top rows, for a hex one tile wide. */
const HEX_ROW = Math.sqrt(3) / 2;
/** Circumradius of that hex: half its height. */
const HEX_RADIUS = 1 / Math.sqrt(3);

/**
 * Pointy-top hexes in odd-r offset storage.
 *
 * Offset storage rather than axial is the whole reason this is affordable: rows
 * and columns stay rectangular, so the level format, the terrain rows, the flat
 * layer arrays, the editor's brushes and every `idx()` in the engine are
 * untouched. Only adjacency, distance and the picture change — and the picture
 * follows from `center`/`outline`, which this tiling answers itself.
 */
class HexGrid implements TacticalGrid {
  readonly id = 'hex';
  readonly name = '六边格';
  readonly directions = HEX_DIRECTIONS;

  distance(a: Coord, b: Coord): number {
    const left = toCube(a);
    const right = toCube(b);
    return Math.max(
      Math.abs(left.q - right.q),
      Math.abs(left.r - right.r),
      Math.abs(left.s - right.s),
    );
  }

  step(at: Coord, direction: Direction): Coord {
    const odd = at.y & 1;
    switch (direction) {
      case 'hexEast': return { x: at.x + 1, y: at.y };
      case 'hexWest': return { x: at.x - 1, y: at.y };
      case 'hexNortheast': return { x: at.x + odd, y: at.y - 1 };
      case 'hexNorthwest': return { x: at.x - 1 + odd, y: at.y - 1 };
      case 'hexSoutheast': return { x: at.x + odd, y: at.y + 1 };
      case 'hexSouthwest': return { x: at.x - 1 + odd, y: at.y + 1 };
      default: throw new RangeError(`grid "hex" has no direction "${direction}"`);
    }
  }

  opposite(direction: Direction): Direction {
    return OPPOSITE[direction] ?? direction;
  }

  toward(from: Coord, to: Coord): Direction {
    // The neighbour that gets closest to the target names the direction; with six
    // neighbours there is no dominant-axis shortcut that stays honest.
    let best = this.directions[0].id;
    let bestDistance = Infinity;
    for (const direction of this.directions) {
      const candidate = this.distance(this.step(from, direction.id), to);
      if (candidate < bestDistance) {
        bestDistance = candidate;
        best = direction.id;
      }
    }
    return best;
  }

  adjacent(at: Coord): Coord[] {
    return this.directions.map((direction) => this.step(at, direction.id));
  }

  within(at: Coord, min: number, max: number): Coord[] {
    const origin = toCube(at);
    const out: Coord[] = [];
    for (let dq = -max; dq <= max; dq++) {
      for (let dr = Math.max(-max, -dq - max); dr <= Math.min(max, -dq + max); dr++) {
        const steps = Math.max(Math.abs(dq), Math.abs(dr), Math.abs(-dq - dr));
        if (steps < min || steps > max) continue;
        out.push(fromCube({ q: origin.q + dq, r: origin.r + dr, s: origin.s - dq - dr }));
      }
    }
    return out;
  }

  line(from: Coord, to: Coord): Coord[] {
    const steps = this.distance(from, to);
    if (steps === 0) return [{ ...from }];
    const start = toCube(from);
    const end = toCube(to);
    const out: Coord[] = [];
    for (let step = 0; step <= steps; step++) {
      const t = step / steps;
      const cube = roundCube(
        start.q + (end.q - start.q) * t,
        start.r + (end.r - start.r) * t,
        start.s + (end.s - start.s) * t,
      );
      out.push(fromCube(cube));
    }
    return out;
  }

  center(at: Coord): TilePoint {
    return { x: at.x + 0.5 + (at.y & 1) * 0.5, y: at.y * HEX_ROW + HEX_RADIUS };
  }

  cellAt(point: TilePoint): Coord {
    // The inverse of `center`: back to fractional cube, then round to a cell.
    const px = point.x - 0.5;
    const py = point.y - HEX_RADIUS;
    const q = px - py / Math.sqrt(3);
    const r = py / HEX_ROW;
    return fromCube(roundCube(q, r, -q - r));
  }

  outline(): readonly TilePoint[] {
    // Pointy-top: a vertex straight up, then every 60°.
    return Array.from({ length: 6 }, (_unused, corner) => {
      const angle = (Math.PI / 180) * (60 * corner - 90);
      return { x: HEX_RADIUS * Math.cos(angle), y: HEX_RADIUS * Math.sin(angle) };
    });
  }

  extent(size: GridExtent): TilePoint {
    return {
      x: size.width + (size.height > 1 ? 0.5 : 0),
      y: (size.height - 1) * HEX_ROW + HEX_RADIUS * 2,
    };
  }
}

/* ------------------------------------------------------------------ registry */

export const TacticalGrids = new ContentRegistry<TacticalGrid>('tactical grid');
TacticalGrids.defineAll([new OrthogonalSquareGrid(), new OctileSquareGrid(), new HexGrid()]);

/** The tiling every level plays on unless it names another. */
export const DEFAULT_GRID_ID = 'square4';

/** The registered tilings of one engine. */
export type GridRegistry = ContentRegistry<TacticalGrid>;

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface GridRules {
  readonly grids: GridRegistry;
}

/**
 * The tiling this battle runs under.
 *
 * Same shape as `activeTurnOrder`: the level names one, the composition provides
 * the implementations, and nothing else in the engine knows the name.
 */
export function activeGrid(rules: GridRules, state: GameState): TacticalGrid {
  return rules.grids.get(state.rules.grid);
}
