import type { FrameAnimationClip } from './frame-animation';

const BOARD_TILE = 32;

export interface RuntimeFrameSheet {
  href: string;
  frameWidth: number;
  frameHeight: number;
  frameCount: number;
  clips?: readonly FrameAnimationClip[];
}

/** Metadata shared by generated runtime sprite sheets and their renderer. */
export interface RuntimeUnitSheet extends RuntimeFrameSheet {
  anchor: { x: number; y: number };
  idleFrame?: number;
  walkFrames?: readonly [number, number];
  attackFrame?: number;
}

/** A fixed-cell atlas used for terrain, structures, icons and effects. */
export interface RuntimeCellAtlas {
  href: string;
  cellWidth: number;
  cellHeight: number;
  columns: number;
  rows: number;
}

/** A regular grid atlas whose cells may have fractional source dimensions. */
export interface RuntimeGridAtlas {
  href: string;
  width: number;
  height: number;
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

function validateFrameSheet(sheet: RuntimeFrameSheet): void {
  finiteInt(sheet.frameWidth, 'frameWidth', 1);
  finiteInt(sheet.frameHeight, 'frameHeight', 1);
  finiteInt(sheet.frameCount, 'frameCount', 1);
}

/**
 * Emit a self-describing horizontal strip. Animation policy lives in metadata;
 * the shared frame system owns timing and the consumer only chooses clip ids.
 */
export function runtimeFrameStripMarkup(
  sheet: RuntimeFrameSheet,
  initialFrame = 0,
  className = 'runtime-frame-strip',
): string {
  validateFrameSheet(sheet);
  finiteInt(initialFrame, 'initialFrame');
  if (initialFrame >= sheet.frameCount) throw new Error(`initialFrame ${initialFrame} exceeds ${sheet.frameCount - 1}`);
  const clips = sheet.clips ?? [];
  const stripWidth = sheet.frameWidth * sheet.frameCount;
  return `<image class="${attr(className)} runtime-frame-strip" href="${attr(sheet.href)}" x="${-initialFrame * sheet.frameWidth}" y="0" width="${stripWidth}" height="${sheet.frameHeight}" preserveAspectRatio="none"
    data-frame-width="${sheet.frameWidth}" data-frame-count="${sheet.frameCount}" data-frame-initial="${initialFrame}" data-frame-clips="${attr(JSON.stringify(clips))}"/>`;
}

/**
 * Render one 32x48 game unit frame inside a 32x32 board cell.
 *
 * The outer <g> deliberately remains the first child of a board unit so the
 * existing face-left transform can mirror the whole sprite. The image itself
 * keeps the complete strip available for CSS-driven walk animation.
 */
export function runtimeUnitMarkup(sheet: RuntimeUnitSheet, team: string, frame = 0): string {
  validateFrameSheet(sheet);
  finiteInt(sheet.anchor.x, 'anchor.x');
  finiteInt(sheet.anchor.y, 'anchor.y');
  finiteInt(frame, 'frame');
  if (frame >= sheet.frameCount) throw new Error(`frame ${frame} exceeds ${sheet.frameCount - 1}`);

  const x = BOARD_TILE / 2 - sheet.anchor.x;
  const y = BOARD_TILE - 1 - sheet.anchor.y;
  const color = attr(team);
  const clipId = `runtime-unit-frame-${sheet.frameWidth}-${sheet.frameHeight}`;
  const idleFrame = sheet.idleFrame ?? 0;
  const walkFrames = sheet.walkFrames ?? [1, 3];
  const attackFrame = sheet.attackFrame ?? Math.min(2, sheet.frameCount - 1);
  for (const [name, value] of [
    ['idleFrame', idleFrame],
    ['walkFrameA', walkFrames[0]],
    ['walkFrameB', walkFrames[1]],
    ['attackFrame', attackFrame],
  ] as const) {
    finiteInt(value, name);
    if (value >= sheet.frameCount) throw new Error(`${name} ${value} exceeds ${sheet.frameCount - 1}`);
  }

  const clips = sheet.clips ?? [
    { id: 'idle', frames: [idleFrame], fps: 1, loop: true },
    { id: 'walk', frames: [...walkFrames], fps: 6.25, loop: true },
    { id: 'attack', frames: [attackFrame], fps: 1, loop: false },
  ];
  return `<g class="sprite-pixel sprite-raster" data-runtime-raster="unit">
    <ellipse class="runtime-unit-contact-shadow" cx="16" cy="29.2" rx="12.5" ry="4.2" fill="#0b100d" opacity="0.48"/>
    <ellipse class="runtime-unit-team-ring" cx="16" cy="29.5" rx="11" ry="2.25" fill="none" stroke="${color}" stroke-width="1.8" opacity="1"/>
    <svg class="runtime-unit-figure" x="${x}" y="${y}" width="${sheet.frameWidth}" height="${sheet.frameHeight}" viewBox="0 0 ${sheet.frameWidth} ${sheet.frameHeight}" overflow="hidden">
      <defs><clipPath id="${clipId}" clipPathUnits="userSpaceOnUse"><rect width="${sheet.frameWidth}" height="${sheet.frameHeight}"/></clipPath></defs>
      <g class="runtime-unit-frame-window" clip-path="url(#${clipId})">
        ${runtimeFrameStripMarkup({ ...sheet, clips }, frame, 'runtime-unit-strip')}
      </g>
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
    <image href="${attr(atlas.href)}" x="${-column * atlas.cellWidth}" y="${-row * atlas.cellHeight}" width="${width}" height="${height}" preserveAspectRatio="none"/>
  </svg>`;
}

/**
 * Crop one cell from an arbitrary regular grid and scale it to presentation
 * size. This is intentionally separate from fixed runtime atlases: generated
 * environment sheets are not guaranteed to divide into integer pixel cells.
 */
export function runtimeGridAtlasCellMarkup(
  atlas: RuntimeGridAtlas,
  cell: number,
  outputWidth: number,
  outputHeight: number,
  className = '',
): string {
  finiteInt(atlas.width, 'width', 1);
  finiteInt(atlas.height, 'height', 1);
  finiteInt(atlas.columns, 'columns', 1);
  finiteInt(atlas.rows, 'rows', 1);
  finiteInt(cell, 'cell');
  finiteInt(outputWidth, 'outputWidth', 1);
  finiteInt(outputHeight, 'outputHeight', 1);
  const capacity = atlas.columns * atlas.rows;
  if (cell >= capacity) throw new Error(`cell ${cell} exceeds grid atlas capacity ${capacity}`);

  const cellWidth = atlas.width / atlas.columns;
  const cellHeight = atlas.height / atlas.rows;
  const column = cell % atlas.columns;
  const row = Math.floor(cell / atlas.columns);
  const viewX = column * cellWidth;
  const viewY = row * cellHeight;
  const classAttr = className ? ` class="${attr(className)}"` : '';
  return `<svg width="${outputWidth}" height="${outputHeight}" viewBox="${viewX} ${viewY} ${cellWidth} ${cellHeight}" overflow="hidden"${classAttr} data-runtime-raster="grid-atlas-cell">
    <image href="${attr(atlas.href)}" x="0" y="0" width="${atlas.width}" height="${atlas.height}" preserveAspectRatio="none"/>
  </svg>`;
}
