import type { TacticalGrid } from '@empire/battle-engine';
export interface SceneInsets {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

export interface SceneViewportProfile {
  insets?: Partial<SceneInsets>;
}

/** Two-pass art surrounding the interactive battlefield. */
export interface SceneFrameMarkup {
  backdrop: string;
  foreground: string;
}

/**
 * Explicit depth contract for art inside the interactive battlefield.
 * Ground can never cover a tactical actor; overUnits is reserved for authored
 * canopies and other silhouettes that intentionally cross the actor plane.
 */
export interface SceneLayerMarkup {
  ground: string;
  underUnits: string;
  overUnits: string;
}

export interface SceneViewport {
  /** The tiling this scene is laid out under; the board asks it where cells go. */
  grid: TacticalGrid;
  tileSize: number;
  fieldWidth: number;
  fieldHeight: number;
  sceneWidth: number;
  sceneHeight: number;
  originX: number;
  originY: number;
  insets: SceneInsets;
}

const finiteNonNegative = (value: number, name: string): number => {
  if (!Number.isFinite(value) || value < 0) throw new Error(`${name} must be a non-negative number`);
  return value;
};

/**
 * Decouples the playable lattice from the authored scene canvas. Insets are
 * non-interactive art space, so a battlefield can have an organic silhouette
 * without changing pathfinding, ranges, saves or deterministic replays.
 *
 * The tiling comes first because the field's size is its answer: a hex board of
 * the same rows and columns is shorter and half a cell wider than a square one,
 * and this module used to multiply both out itself.
 */
export function createSceneViewport(
  grid: TacticalGrid,
  columns: number,
  rows: number,
  tileSize: number,
  profile: SceneViewportProfile = {},
): SceneViewport {
  if (!Number.isInteger(columns) || columns < 1) throw new Error('columns must be a positive integer');
  if (!Number.isInteger(rows) || rows < 1) throw new Error('rows must be a positive integer');
  finiteNonNegative(tileSize, 'tileSize');
  if (tileSize === 0) throw new Error('tileSize must be greater than zero');
  const insets: SceneInsets = {
    top: finiteNonNegative(profile.insets?.top ?? 0, 'insets.top'),
    right: finiteNonNegative(profile.insets?.right ?? 0, 'insets.right'),
    bottom: finiteNonNegative(profile.insets?.bottom ?? 0, 'insets.bottom'),
    left: finiteNonNegative(profile.insets?.left ?? 0, 'insets.left'),
  };
  const extent = grid.extent({ width: columns, height: rows });
  const fieldWidth = extent.x * tileSize;
  const fieldHeight = extent.y * tileSize;
  return Object.freeze({
    grid,
    tileSize,
    fieldWidth,
    fieldHeight,
    sceneWidth: insets.left + fieldWidth + insets.right,
    sceneHeight: insets.top + fieldHeight + insets.bottom,
    originX: insets.left,
    originY: insets.top,
    insets: Object.freeze(insets),
  });
}

export function scenePointToCell(
  viewport: SceneViewport,
  sceneX: number,
  sceneY: number,
): { x: number; y: number } | null {
  const localX = sceneX - viewport.originX;
  const localY = sceneY - viewport.originY;
  if (localX < 0 || localY < 0 || localX >= viewport.fieldWidth || localY >= viewport.fieldHeight) {
    return null;
  }
  // Which cell a point falls in is the tiling's own question, and its answer is
  // the inverse of where the tiling puts that cell.
  return viewport.grid.cellAt({ x: localX / viewport.tileSize, y: localY / viewport.tileSize });
}

/** Top-left of a cell's bounding box, in scene units. */
export function cellOrigin(viewport: SceneViewport, at: { x: number; y: number }): { x: number; y: number } {
  const centre = viewport.grid.center(at);
  return { x: (centre.x - 0.5) * viewport.tileSize, y: (centre.y - 0.5) * viewport.tileSize };
}

/** Centre of a cell, in scene units. */
export function cellCenter(viewport: SceneViewport, at: { x: number; y: number }): { x: number; y: number } {
  const centre = viewport.grid.center(at);
  return { x: centre.x * viewport.tileSize, y: centre.y * viewport.tileSize };
}

/** Outline of one cell as an SVG points list, centred on that cell. */
export function cellOutline(viewport: SceneViewport, at: { x: number; y: number }): string {
  const centre = cellCenter(viewport, at);
  return viewport.grid.outline()
    .map((corner) => `${(centre.x + corner.x * viewport.tileSize).toFixed(2)},${(centre.y + corner.y * viewport.tileSize).toFixed(2)}`)
    .join(' ');
}
