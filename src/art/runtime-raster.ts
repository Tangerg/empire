const BOARD_TILE = 32;

/** Metadata shared by generated runtime sprite sheets and their renderer. */
export interface RuntimeUnitSheet {
  href: string;
  frameWidth: number;
  frameHeight: number;
  frameCount: number;
  anchor: { x: number; y: number };
}

/** A fixed-cell atlas used for terrain, structures, icons and effects. */
export interface RuntimeCellAtlas {
  href: string;
  cellWidth: number;
  cellHeight: number;
  columns: number;
  rows: number;
}

const attr = (value: string): string =>
  value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');

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
 * Render one 32x48 game unit frame inside a 32x32 board cell.
 *
 * The outer <g> deliberately remains the first child of a board unit so the
 * existing face-left transform can mirror the whole sprite. The image itself
 * keeps the complete strip available for CSS-driven walk animation.
 */
export function runtimeUnitMarkup(sheet: RuntimeUnitSheet, team: string, frame = 0): string {
  finiteInt(sheet.frameWidth, 'frameWidth', 1);
  finiteInt(sheet.frameHeight, 'frameHeight', 1);
  finiteInt(sheet.frameCount, 'frameCount', 1);
  finiteInt(sheet.anchor.x, 'anchor.x');
  finiteInt(sheet.anchor.y, 'anchor.y');
  finiteInt(frame, 'frame');
  if (frame >= sheet.frameCount) throw new Error(`frame ${frame} exceeds ${sheet.frameCount - 1}`);

  const x = BOARD_TILE / 2 - sheet.anchor.x;
  const y = BOARD_TILE - 1 - sheet.anchor.y;
  const sheetWidth = sheet.frameWidth * sheet.frameCount;
  const href = attr(sheet.href);
  const color = attr(team);

  return `<g class="sprite-pixel sprite-raster" shape-rendering="crispEdges" data-runtime-raster="unit">
    <ellipse cx="16" cy="29.5" rx="11" ry="2.25" fill="none" stroke="${color}" stroke-width="1.5" opacity="0.95"/>
    <svg x="${x}" y="${y}" width="${sheet.frameWidth}" height="${sheet.frameHeight}" viewBox="0 0 ${sheet.frameWidth} ${sheet.frameHeight}" overflow="hidden">
      <image class="runtime-unit-strip" href="${href}" x="${-frame * sheet.frameWidth}" y="0" width="${sheetWidth}" height="${sheet.frameHeight}" preserveAspectRatio="none" image-rendering="pixelated"/>
    </svg>
  </g>`;
}

/** Render one atlas cell without resampling, clipped to the requested viewport. */
export function runtimeAtlasCellMarkup(atlas: RuntimeCellAtlas, cell: number): string {
  validateAtlas(atlas);
  finiteInt(cell, 'cell');
  const capacity = atlas.columns * atlas.rows;
  if (cell >= capacity) throw new Error(`cell ${cell} exceeds atlas capacity ${capacity}`);

  const column = cell % atlas.columns;
  const row = Math.floor(cell / atlas.columns);
  const width = atlas.cellWidth * atlas.columns;
  const height = atlas.cellHeight * atlas.rows;
  return `<svg width="${atlas.cellWidth}" height="${atlas.cellHeight}" viewBox="0 0 ${atlas.cellWidth} ${atlas.cellHeight}" overflow="hidden" shape-rendering="crispEdges" data-runtime-raster="atlas-cell">
    <image href="${attr(atlas.href)}" x="${-column * atlas.cellWidth}" y="${-row * atlas.cellHeight}" width="${width}" height="${height}" preserveAspectRatio="none" image-rendering="pixelated"/>
  </svg>`;
}
