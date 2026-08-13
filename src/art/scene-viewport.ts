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
 */
export function createSceneViewport(
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
  const fieldWidth = columns * tileSize;
  const fieldHeight = rows * tileSize;
  return Object.freeze({
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
  return {
    x: Math.floor(localX / viewport.tileSize),
    y: Math.floor(localY / viewport.tileSize),
  };
}
