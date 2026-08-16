import type { Coord, Direction, GameMap } from '@empire/battle-engine';
import { PAL } from './palette';
import { TILE } from './terrain';

/** A tile the board wants to tint, and how strongly. */
export interface DecoratedCell {
  readonly x: number;
  readonly y: number;
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
  /** The cell's own outline as an SVG points list. */
  outline(at: Coord): string;
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
  /** Drawn once with the map. Empty for art that already shows its own ground. */
  gridLines(layout: BoardLayout, map: GameMap): string;
  /** One tile of move range, threat, healing or a marked blast. */
  actionSpot(layout: BoardLayout, cell: DecoratedCell): string;
  /** The march order, from tile centre to tile centre. */
  movePath(layout: BoardLayout, points: readonly Coord[]): string;
  ring(layout: BoardLayout, at: Coord, kind: 'selection' | 'cursor'): string;
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
    // rather than one outline per cell. Anything else draws its own edges.
    if (layout.corners !== 4) {
      return [...everyCell(map)].map((cell) =>
        `<polygon points="${layout.outline(cell)}" fill="none" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.14"/>`,
      ).join('');
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
    return parts.join('');
  },
  actionSpot: (layout, { x, y, fill, opacity, stroke }) => {
    const origin = layout.origin({ x, y });
    if (layout.corners !== 4) {
      return `<polygon points="${layout.outline({ x, y })}" fill="${fill}" fill-opacity="${opacity}"${outlineOf(stroke)}/>`;
    }
    return `<rect x="${origin.x + 2}" y="${origin.y + 2}" width="${layout.tileSize - 4}" height="${layout.tileSize - 4}" rx="7" fill="${fill}" fill-opacity="${opacity}"${outlineOf(stroke)}/>`;
  },
  movePath: (layout, points) => centres(layout, points)
    .map((point, index) => `${index === 0 ? 'M' : 'L'}${point.x} ${point.y}`).join(' '),
  ring: (layout, at, kind) => {
    if (layout.corners !== 4) {
      const stroke = kind === 'selection' ? '#ffffff' : PAL.gold;
      return `<polygon points="${layout.outline(at)}" fill="none" stroke="${stroke}" stroke-width="${kind === 'selection' ? 2 : 1.6}" opacity="0.95"/>`;
    }
    const origin = layout.origin(at);
    const size = layout.tileSize;
    return kind === 'selection'
      ? `<rect x="${origin.x + 1}" y="${origin.y + 1}" width="${size - 2}" height="${size - 2}" fill="none" stroke="#ffffff" stroke-width="2" rx="3" opacity="0.95"/>`
      : `<rect x="${origin.x + 0.5}" y="${origin.y + 0.5}" width="${size - 1}" height="${size - 1}" fill="none" stroke="${PAL.gold}" stroke-width="1.6" rx="2"/>`;
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
  gridLines: (layout, map) => [...everyCell(map)].map((cell) => {
    const centre = layout.center(cell);
    return `<circle class="candidate-stand-node" cx="${centre.x}" cy="${centre.y}" r="2.2" fill="#fff4dc" opacity="0.32"/>`;
  }).join(''),
  actionSpot: (layout, { x, y, fill, opacity, stroke }) => {
    const centre = layout.center({ x, y });
    return `<ellipse class="candidate-action-spot" cx="${centre.x}" cy="${centre.y + layout.tileSize * 0.18}" rx="12.5" ry="7.5" fill="${fill}" fill-opacity="${Math.min(0.5, opacity * 1.35)}"${outlineOf(stroke)}/>`;
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
  ring: (layout, at, kind) => {
    const centre = layout.center(at);
    return kind === 'selection'
      ? `<ellipse class="candidate-selection-ring" cx="${centre.x}" cy="${centre.y + 11}" rx="13" ry="5.5" fill="none" stroke="#ffffff" stroke-width="2" opacity="0.96"/>`
      : `<ellipse class="candidate-cursor-ring" cx="${centre.x}" cy="${centre.y + 10}" rx="13.5" ry="6" fill="none" stroke="${PAL.gold}" stroke-width="1.8"/>`;
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
  outline: (at) => [
    `${at.x * TILE},${at.y * TILE}`,
    `${at.x * TILE + TILE},${at.y * TILE}`,
    `${at.x * TILE + TILE},${at.y * TILE + TILE}`,
    `${at.x * TILE},${at.y * TILE + TILE}`,
  ].join(' '),
};
