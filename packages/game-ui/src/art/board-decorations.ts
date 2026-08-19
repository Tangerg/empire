import type { Coord, Direction, GameMap } from '@empire/battle-engine';
import { wholeField, type BoardPiece } from './board-surface';
import { PAL } from './palette';
import { TILE } from './terrain';

/**
 * How strongly the board wants a tile tinted.
 *
 * It used to carry the cell as well, because `actionSpot` drew at absolute scene
 * coordinates. A tint is not a place.
 */
export interface DecorationTint {
  readonly fill: string;
  readonly opacity: number;
  readonly stroke?: string;
}

/**
 * Where the cells of this board are, in scene units.
 *
 * Decorations used to compute `x * TILE` themselves, which is a four-way square
 * board written into the tactical overlay: on a hex board every one of them
 * would have drawn in the wrong place. The layout comes from the tiling, so the
 * grid lines, the move range and the cursor land wherever the cells actually are.
 */
export interface BoardLayout {
  readonly tileSize: number;
  /** Corners a cell has: four for squares, six for hexes. */
  readonly corners: number;
  /** Top-left of a cell's bounding box. */
  origin(at: Coord): { x: number; y: number };
  center(at: Coord): { x: number; y: number };
  /**
   * A cell's outline as an SVG points list, about the cell's own origin.
   *
   * One shape, not one per cell: the tiling answers `outline()` without being told
   * which cell, because every cell of a tiling has the same shape. This used to
   * take a cell and return the shape already moved there, which is how four
   * callers ended up with a picture they could not place anywhere else.
   */
  shape(): string;
  /**
   * Centre of the cell one step away, so an edge between the two can be drawn
   * without knowing which tiling this is or what its facings are called.
   */
  neighbour(at: Coord, direction: Direction): { x: number; y: number };
}

/**
 * The line between two cells, as the tactical layer draws a cliff or a cover edge.
 *
 * `reach` is how far along from the cell's own centre the line sits: a cliff
 * belongs to both cells and sits on the boundary, while a cover edge belongs to
 * the cell that put it up and sits just inside, so two facing walls stay two
 * lines instead of one.
 */
export function edgeLine(
  layout: BoardLayout,
  at: Coord,
  toward: { x: number; y: number },
  color: string,
  reach = 0.5,
): string {
  const from = layout.center(at);
  const dx = toward.x - from.x;
  const dy = toward.y - from.y;
  const span = Math.hypot(dx, dy);
  if (span === 0) return '';
  const cx = from.x + dx * reach;
  const cy = from.y + dy * reach;
  // Perpendicular to the line joining the two centres, and a little shorter than
  // the gap between them, so neighbouring edges do not run into one another.
  const half = span * 0.42;
  const nx = -dy / span * half;
  const ny = dx / span * half;
  return `<line x1="${(cx - nx).toFixed(2)}" y1="${(cy - ny).toFixed(2)}" ` +
    `x2="${(cx + nx).toFixed(2)}" y2="${(cy + ny).toFixed(2)}" stroke="${color}" stroke-width="3"/>`;
}

/**
 * How a board draws the tactical layer over whatever art is underneath it.
 *
 * The board used to ask the presentation for its own id in six places to choose
 * between squares and ground-level ellipses. That is a strategy object being
 * asked for its name instead of its behaviour: a third presentation got the
 * authored look whether it wanted it or not, and could not mix — authored
 * scenery with square tiles was unreachable.
 */
export interface BoardDecorations {
  readonly id: string;
  /** Crisp pixels suit a grid; painted scenes do not want them. */
  readonly shapeRendering: string;
  /**
   * Drawn once with the map. Empty for art that already shows its own ground.
   *
   * Pieces rather than one string because the two shipped looks are different
   * shapes of answer: a square lattice is two families of lines across the whole
   * field, while a painted scene seats one identical node under every cell.
   */
  gridLines(layout: BoardLayout, map: GameMap): readonly BoardPiece[];
  /** One tile of move range, threat, healing or a marked blast, at a cell's origin. */
  actionSpot(layout: BoardLayout, tint: DecorationTint): string;
  /** The march order, from tile centre to tile centre — one line through many cells. */
  movePath(layout: BoardLayout, points: readonly Coord[]): string;
  /** The ring around a selected or hovered cell, at that cell's origin. */
  ring(layout: BoardLayout, kind: 'selection' | 'cursor'): string;
}

const outlineOf = (stroke?: string): string =>
  stroke ? ` stroke="${stroke}" stroke-width="1" stroke-opacity="0.78"` : '';

const centres = (layout: BoardLayout, points: readonly Coord[]): Array<{ x: number; y: number }> =>
  points.map((cell) => layout.center(cell));

/** Every cell of the board, for a decoration that covers the whole field. */
function* everyCell(map: GameMap): Generator<Coord> {
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) yield { x, y };
  }
}

/** The default look: a legible grid of squares over flat terrain tiles. */
export const SquareBoardDecorations: BoardDecorations = {
  id: 'square',
  shapeRendering: 'crispEdges',
  gridLines: (layout, map) => {
    // A four-cornered cell tiles into a lattice, which is two families of lines
    // rather than one outline per cell. Anything else draws its own edges — the
    // same picture at every cell, which is why those are pieces.
    if (layout.corners !== 4) {
      const cell = `<polygon points="${layout.shape()}" fill="none" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.14"/>`;
      return [...everyCell(map)].map((at) => ({ markup: cell, ...layout.origin(at) }));
    }
    const parts: string[] = [];
    const size = layout.tileSize;
    for (let x = 1; x < map.width; x++) {
      parts.push(
        `<line x1="${x * size}" y1="0" x2="${x * size}" y2="${map.height * size}" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.12"/>`,
      );
    }
    for (let y = 1; y < map.height; y++) {
      parts.push(
        `<line x1="0" y1="${y * size}" x2="${map.width * size}" y2="${y * size}" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.12"/>`,
      );
    }
    return wholeField(parts.join(''));
  },
  actionSpot: (layout, { fill, opacity, stroke }) => {
    if (layout.corners !== 4) {
      return `<polygon points="${layout.shape()}" fill="${fill}" fill-opacity="${opacity}"${outlineOf(stroke)}/>`;
    }
    return `<rect x="2" y="2" width="${layout.tileSize - 4}" height="${layout.tileSize - 4}" rx="7" fill="${fill}" fill-opacity="${opacity}"${outlineOf(stroke)}/>`;
  },
  movePath: (layout, points) => centres(layout, points)
    .map((point, index) => `${index === 0 ? 'M' : 'L'}${point.x} ${point.y}`).join(' '),
  ring: (layout, kind) => {
    if (layout.corners !== 4) {
      const stroke = kind === 'selection' ? '#ffffff' : PAL.gold;
      return `<polygon points="${layout.shape()}" fill="none" stroke="${stroke}" stroke-width="${kind === 'selection' ? 2 : 1.6}" opacity="0.95"/>`;
    }
    const size = layout.tileSize;
    return kind === 'selection'
      ? `<rect x="1" y="1" width="${size - 2}" height="${size - 2}" fill="none" stroke="#ffffff" stroke-width="2" rx="3" opacity="0.95"/>`
      : `<rect x="0.5" y="0.5" width="${size - 1}" height="${size - 1}" fill="none" stroke="${PAL.gold}" stroke-width="1.6" rx="2"/>`;
  },
};

/**
 * The authored look: ellipses that sit on the ground of a painted scene.
 *
 * Legal standing positions appear only during tactical interaction, so the
 * landscape does not turn back into a visible spreadsheet.
 */
export const GroundBoardDecorations: BoardDecorations = {
  id: 'ground',
  shapeRendering: 'geometricPrecision',
  /**
   * One stand node per cell, seated by a shape rather than by a filter.
   *
   * These carried `filter: drop-shadow(0 1px 1px …)` in the stylesheet, which on a
   * shipped map is 459 separate blur passes for a 2.2px dot — recomputed every
   * time the board is rescaled. The shadow it was imitating is one more circle,
   * which costs nothing to raster and looks the same.
   */
  gridLines: (layout, map) => {
    const middle = layout.tileSize / 2;
    // One node, seated at every cell. It used to be one string of N nodes with N
    // pairs of absolute coordinates baked in, which is the same picture written out
    // four thousand times. The group that used to hold the two circles together is
    // gone with them: a piece already has a group, nothing styled that class, and
    // keeping it would have cost the DOM one node per cell for nothing.
    const node =
      `<circle cx="${middle}" cy="${middle + 1}" r="2.2" fill="#000000" opacity="0.22"/>`
      + `<circle cx="${middle}" cy="${middle}" r="2.2" fill="#fff4dc" opacity="0.32"/>`;
    return [...everyCell(map)].map((at) => ({ markup: node, ...layout.origin(at) }));
  },
  actionSpot: (layout, { fill, opacity, stroke }) => {
    const middle = layout.tileSize / 2;
    return `<ellipse class="candidate-action-spot" cx="${middle}" cy="${middle + layout.tileSize * 0.18}" rx="12.5" ry="7.5" fill="${fill}" fill-opacity="${Math.min(0.5, opacity * 1.35)}"${outlineOf(stroke)}/>`;
  },
  movePath: (layout, points) => {
    const spots = centres(layout, points);
    return spots.slice(0, -1).reduce((value, p1, index) => {
      const p0 = spots[Math.max(0, index - 1)];
      const p2 = spots[index + 1];
      const p3 = spots[Math.min(spots.length - 1, index + 2)];
      return `${value} C${(p1.x + (p2.x - p0.x) / 6).toFixed(1)} ${(p1.y + (p2.y - p0.y) / 6).toFixed(1)} ${(p2.x - (p3.x - p1.x) / 6).toFixed(1)} ${(p2.y - (p3.y - p1.y) / 6).toFixed(1)} ${p2.x} ${p2.y}`;
    }, `M${spots[0].x} ${spots[0].y}`);
  },
  ring: (layout, kind) => {
    const middle = layout.tileSize / 2;
    return kind === 'selection'
      ? `<ellipse class="candidate-selection-ring" cx="${middle}" cy="${middle + 11}" rx="13" ry="5.5" fill="none" stroke="#ffffff" stroke-width="2" opacity="0.96"/>`
      : `<ellipse class="candidate-cursor-ring" cx="${middle}" cy="${middle + 10}" rx="13.5" ry="6" fill="none" stroke="${PAL.gold}" stroke-width="1.8"/>`;
  },
};

/** The layout a square board has always had, for a caller without a viewport. */
const SQUARE_STEPS: Record<string, Coord> = {
  north: { x: 0, y: -1 },
  east: { x: 1, y: 0 },
  south: { x: 0, y: 1 },
  west: { x: -1, y: 0 },
};

export const squareLayout: BoardLayout = {
  tileSize: TILE,
  corners: 4,
  origin: (at) => ({ x: at.x * TILE, y: at.y * TILE }),
  center: (at) => ({ x: at.x * TILE + TILE / 2, y: at.y * TILE + TILE / 2 }),
  neighbour: (at, direction) => {
    const step = SQUARE_STEPS[direction] ?? { x: 0, y: 0 };
    return { x: (at.x + step.x) * TILE + TILE / 2, y: (at.y + step.y) * TILE + TILE / 2 };
  },
  shape: () => `0,0 ${TILE},0 ${TILE},${TILE} 0,${TILE}`,
};
