/**
 * Where a pointer is, in scene units, for a picture letterboxed inside its box.
 *
 * Shared by every backend rather than reimplemented per renderer: two surfaces that
 * disagree about where a click landed would be two different games. The scale is
 * the smaller of the two ratios and the leftover is split evenly, which is what an
 * SVG `viewBox` does by default and what a GPU backend has to be told to do.
 */
export interface SceneBox {
  readonly width: number;
  readonly height: number;
}

export interface ShownAt {
  /** Scene units per presented pixel. */
  readonly scale: number;
  /** Where the picture's own origin sits inside the box. */
  readonly left: number;
  readonly top: number;
}

/** How a scene of this size is shown inside a box of that size. */
export function shownAt(box: { width: number; height: number }, scene: SceneBox): ShownAt | null {
  if (box.width <= 0 || box.height <= 0) return null;
  const scale = Math.min(box.width / scene.width, box.height / scene.height);
  return {
    scale,
    left: (box.width - scene.width * scale) / 2,
    top: (box.height - scene.height * scale) / 2,
  };
}

/** A point on the page, as a point in the scene, or `null` if nothing is shown. */
export function scenePointOf(
  box: { left: number; top: number; width: number; height: number },
  scene: SceneBox,
  page: { clientX: number; clientY: number },
): { x: number; y: number } | null {
  const shown = shownAt(box, scene);
  if (!shown) return null;
  return {
    x: (page.clientX - box.left - shown.left) / shown.scale,
    y: (page.clientY - box.top - shown.top) / shown.scale,
  };
}
