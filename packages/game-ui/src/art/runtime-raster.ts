import type { BoardPicture } from './board-surface';
import { escapeAttr as attr } from './svg';

const BOARD_TILE = 32;

/**
 * A generated unit spritesheet: one row of frames, and what they are for.
 *
 * There was a `RuntimeFrameSheet` above this that added an optional `clips` array,
 * for a caller that would have had to know the three names `BoardView` plays a unit
 * through — and no producer ever set it. A unit's clips are these three.
 */
export interface RuntimeUnitSheet {
  href: string;
  frameWidth: number;
  frameHeight: number;
  frameCount: number;
  anchor: { x: number; y: number };
  idleFrame: number;
  walkFrames: readonly [number, number];
  attackFrame: number;
}

/** A fixed-cell atlas used for terrain, structures, icons and effects. */
export interface RuntimeCellAtlas {
  href: string;
  cellWidth: number;
  cellHeight: number;
  columns: number;
  rows: number;
}

const finiteInt = (value: number, name: string, min = 0): number => {
  if (!Number.isInteger(value) || value < min) throw new Error(`${name} must be an integer >= ${min}`);
  return value;
};

function validateAtlas(atlas: RuntimeCellAtlas): void {
  finiteInt(atlas.cellWidth, 'cellWidth', 1);
  finiteInt(atlas.cellHeight, 'cellHeight', 1);
  finiteInt(atlas.columns, 'columns', 1);
  finiteInt(atlas.rows, 'rows', 1);
}

/**
 * One generated unit in a 32x32 board cell: its ground marks, and its strip.
 *
 * This used to be one markup string with the strip nested three elements deep
 * inside it — a `<g>` for the mirror, a nested `<svg>` for the viewport, a
 * `clipPath` for the same clipping a second time, and the strip's own description
 * serialised into `data-frame-*` attributes for the renderer to read back. The
 * clip path's id was fixed per frame size, so every unit of a given size put the
 * *same* `id` in the document and every reference resolved to whichever came first.
 * It worked because they were identical.
 *
 * Two declared halves instead. The body is what belongs to the board — a contact
 * shadow and a team ring, in board coordinates. The strip is the sheet, where it
 * sits, and what may be played on it: the three clips `BoardView` names.
 */
export function runtimeUnitPicture(sheet: RuntimeUnitSheet, team: string): BoardPicture {
  finiteInt(sheet.frameWidth, 'frameWidth', 1);
  finiteInt(sheet.frameHeight, 'frameHeight', 1);
  finiteInt(sheet.frameCount, 'frameCount', 1);
  finiteInt(sheet.anchor.x, 'anchor.x');
  finiteInt(sheet.anchor.y, 'anchor.y');

  const { idleFrame, walkFrames, attackFrame } = sheet;
  for (const [name, value] of [
    ['idleFrame', idleFrame],
    ['walkFrameA', walkFrames[0]],
    ['walkFrameB', walkFrames[1]],
    ['attackFrame', attackFrame],
  ] as const) {
    finiteInt(value, name);
    if (value >= sheet.frameCount) throw new Error(`${name} ${value} exceeds ${sheet.frameCount - 1}`);
  }

  const color = attr(team);
  return {
    body: `<g class="sprite-pixel" data-runtime-raster="unit">
    <ellipse class="runtime-unit-contact-shadow" cx="16" cy="29.2" rx="12.5" ry="4.2" fill="#0b100d" opacity="0.48"/>
    <ellipse class="runtime-unit-team-ring" cx="16" cy="29.5" rx="11" ry="2.25" fill="none" stroke="${color}" stroke-width="1.8" opacity="1"/>
  </g>`,
    strip: {
      href: sheet.href,
      frameWidth: sheet.frameWidth,
      frameHeight: sheet.frameHeight,
      frameCount: sheet.frameCount,
      // The sheet's anchor is where the figure's feet are; the board's is the
      // middle of the cell, one pixel off its floor.
      x: BOARD_TILE / 2 - sheet.anchor.x,
      y: BOARD_TILE - 1 - sheet.anchor.y,
      clips: [
        { id: 'idle', frames: [idleFrame], fps: 1, loop: true },
        { id: 'walk', frames: [...walkFrames], fps: 6.25, loop: true },
        { id: 'attack', frames: [attackFrame], fps: 1, loop: false },
      ],
      playing: 'idle',
    },
  };
}

/** How a cell is fitted into the box it is drawn in. */
export interface RuntimeTileFit {
  /**
   * How far the drawing spills past its box, in cell units.
   *
   * A cell drawn at exactly its own box leaves a hairline where it meets its
   * neighbour: the board is presented at whatever scale the window allows, so a
   * cell's edge lands on a fraction of a device pixel and the browser antialiases
   * the clip. Two neighbours each covering 70% of the shared edge leave 30% of
   * what is behind them showing through, once per boundary — a lattice over the
   * whole field, which is the third way this field has grown one.
   */
  readonly bleed?: number;
  /**
   * How much of each edge of the *source* cell to leave out, in cell units.
   *
   * For a sheet whose cells carry a baked border. Tiling such a cell draws that
   * border once per boundary whatever the renderer does — it is in the art, and it
   * shows at exactly 1:1 with no filtering at all. Sampling the inside of the cell
   * and stretching it over the box drops the border; the cost is that the cell is
   * magnified by `cellWidth / (cellWidth - 2 * inset)`, which organic ground can
   * afford and a road connecting to its neighbour cannot.
   */
  readonly inset?: number;
}

/** One atlas cell, fitted into a box of the cell's own size. */
function atlasCell(atlas: RuntimeCellAtlas, cell: number, fit: RuntimeTileFit): string {
  validateAtlas(atlas);
  finiteInt(cell, 'cell');
  const capacity = atlas.columns * atlas.rows;
  if (cell >= capacity) throw new Error(`cell ${cell} exceeds atlas capacity ${capacity}`);
  const { bleed = 0, inset = 0 } = fit;
  if (inset * 2 >= Math.min(atlas.cellWidth, atlas.cellHeight)) {
    throw new Error(`inset ${inset} leaves nothing of a ${atlas.cellWidth}x${atlas.cellHeight} cell`);
  }

  const column = cell % atlas.columns;
  const row = Math.floor(cell / atlas.columns);
  // The magnification that maps the cell's kept interior onto the whole box.
  const zoomX = atlas.cellWidth / (atlas.cellWidth - inset * 2);
  const zoomY = atlas.cellHeight / (atlas.cellHeight - inset * 2);
  const width = atlas.cellWidth * atlas.columns * zoomX;
  const height = atlas.cellHeight * atlas.rows * zoomY;
  const x = -(column * atlas.cellWidth + inset) * zoomX;
  const y = -(row * atlas.cellHeight + inset) * zoomY;
  const box = bleed === 0
    ? `width="${atlas.cellWidth}" height="${atlas.cellHeight}"`
    : `x="${-bleed}" y="${-bleed}" width="${atlas.cellWidth + bleed * 2}" height="${atlas.cellHeight + bleed * 2}"`;
  return `<svg ${box} viewBox="0 0 ${atlas.cellWidth} ${atlas.cellHeight}" overflow="hidden" shape-rendering="crispEdges" data-runtime-raster="atlas-cell">
    <image href="${attr(atlas.href)}" x="${x}" y="${y}" width="${width}" height="${height}" preserveAspectRatio="none"/>
  </svg>`;
}

/** Render one atlas cell without resampling, clipped to the requested viewport. */
export const runtimeAtlasCellMarkup = (atlas: RuntimeCellAtlas, cell: number): string =>
  atlasCell(atlas, cell, {});

/**
 * One atlas cell drawn as a tile in a continuous field.
 *
 * Only the ground is drawn this way: a structure or an icon has no neighbour to
 * meet, and both of `RuntimeTileFit`'s adjustments would move it.
 */
export const runtimeTileMarkup = (
  atlas: RuntimeCellAtlas,
  cell: number,
  fit: RuntimeTileFit = { bleed: 0.5 },
): string => atlasCell(atlas, cell, fit);
