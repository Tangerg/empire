import type { Coord, GameMap } from '@empire/battle-engine/types';
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
  gridLines(map: GameMap): string;
  /** One tile of move range, threat, healing or a marked blast. */
  actionSpot(cell: DecoratedCell): string;
  /** The march order, from tile centre to tile centre. */
  movePath(points: readonly Coord[]): string;
  ring(at: Coord, kind: 'selection' | 'cursor'): string;
}

const outlineOf = (stroke?: string): string =>
  stroke ? ` stroke="${stroke}" stroke-width="1" stroke-opacity="0.78"` : '';

const centres = (points: readonly Coord[]): Coord[] =>
  points.map((cell) => ({ x: cell.x * TILE + TILE / 2, y: cell.y * TILE + TILE / 2 }));

/** The default look: a legible grid of squares over flat terrain tiles. */
export const SquareBoardDecorations: BoardDecorations = {
  id: 'square',
  shapeRendering: 'crispEdges',
  gridLines: ({ width, height }) => {
    const parts: string[] = [];
    for (let x = 1; x < width; x++) {
      parts.push(
        `<line x1="${x * TILE}" y1="0" x2="${x * TILE}" y2="${height * TILE}" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.12"/>`,
      );
    }
    for (let y = 1; y < height; y++) {
      parts.push(
        `<line x1="0" y1="${y * TILE}" x2="${width * TILE}" y2="${y * TILE}" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.12"/>`,
      );
    }
    return parts.join('');
  },
  actionSpot: ({ x, y, fill, opacity, stroke }) =>
    `<rect x="${x * TILE + 2}" y="${y * TILE + 2}" width="${TILE - 4}" height="${TILE - 4}" rx="7" fill="${fill}" fill-opacity="${opacity}"${outlineOf(stroke)}/>`,
  movePath: (points) => centres(points).map((point, index) => `${index === 0 ? 'M' : 'L'}${point.x} ${point.y}`).join(' '),
  ring: (at, kind) => kind === 'selection'
    ? `<rect x="${at.x * TILE + 1}" y="${at.y * TILE + 1}" width="${TILE - 2}" height="${TILE - 2}" fill="none" stroke="#ffffff" stroke-width="2" rx="3" opacity="0.95"/>`
    : `<rect x="${at.x * TILE + 0.5}" y="${at.y * TILE + 0.5}" width="${TILE - 1}" height="${TILE - 1}" fill="none" stroke="${PAL.gold}" stroke-width="1.6" rx="2"/>`,
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
  gridLines: ({ width, height }) => {
    const parts: string[] = [];
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        parts.push(`<circle class="candidate-stand-node" cx="${x * TILE + TILE / 2}" cy="${y * TILE + TILE / 2}" r="2.2" fill="#fff4dc" opacity="0.32"/>`);
      }
    }
    return parts.join('');
  },
  actionSpot: ({ x, y, fill, opacity, stroke }) =>
    `<ellipse class="candidate-action-spot" cx="${x * TILE + TILE / 2}" cy="${y * TILE + TILE * 0.68}" rx="12.5" ry="7.5" fill="${fill}" fill-opacity="${Math.min(0.5, opacity * 1.35)}"${outlineOf(stroke)}/>`,
  movePath: (points) => {
    const spots = centres(points);
    return spots.slice(0, -1).reduce((value, p1, index) => {
      const p0 = spots[Math.max(0, index - 1)];
      const p2 = spots[index + 1];
      const p3 = spots[Math.min(spots.length - 1, index + 2)];
      return `${value} C${(p1.x + (p2.x - p0.x) / 6).toFixed(1)} ${(p1.y + (p2.y - p0.y) / 6).toFixed(1)} ${(p2.x - (p3.x - p1.x) / 6).toFixed(1)} ${(p2.y - (p3.y - p1.y) / 6).toFixed(1)} ${p2.x} ${p2.y}`;
    }, `M${spots[0].x} ${spots[0].y}`);
  },
  ring: (at, kind) => kind === 'selection'
    ? `<ellipse class="candidate-selection-ring" cx="${at.x * TILE + TILE / 2}" cy="${at.y * TILE + 27}" rx="13" ry="5.5" fill="none" stroke="#ffffff" stroke-width="2" opacity="0.96"/>`
    : `<ellipse class="candidate-cursor-ring" cx="${at.x * TILE + TILE / 2}" cy="${at.y * TILE + 26}" rx="13.5" ry="6" fill="none" stroke="${PAL.gold}" stroke-width="1.8"/>`,
};
