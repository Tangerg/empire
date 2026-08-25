import { PAL } from './palette';

/**
 * The shadow a thing standing in a cell casts on the ground under it.
 *
 * Twelve of these were written out by hand across four modules, every one an
 * ellipse filled `PAL.ink`, at eleven different opacities between 0.12 and 0.34.
 * That is not eleven artistic decisions — nobody chose 0.22 over 0.24 for a
 * reason — it is one idiom typed twelve times with the strength jittered, and the
 * jitter is what made it look like a choice.
 *
 * The size stays the caller's, because it is real information: a keep's footing
 * is not a soldier's, and a scenery prop drawn at half scale has a shadow half as
 * wide. How dark the ground goes is not information about the thing; it is one
 * three-step scale, and three steps is what a reader can hold.
 */
export type ShadowWeight = 'light' | 'normal' | 'heavy';

/** Alpha per step. Stated once so the steps stay a scale rather than a spread. */
const WEIGHT_ALPHA: Readonly<Record<ShadowWeight, number>> = {
  /** Something spread thin over the ground: a marsh, a low shrub. */
  light: 0.18,
  /** A thing standing in the cell: most terrain, most props. */
  normal: 0.22,
  /** A thing with mass on it: a wall, a keep, a unit. */
  heavy: 0.3,
};

/**
 * Where a unit's feet are inside its cell, and how wide its stance reads.
 *
 * One fact with two readers: the team ring a raster unit stands in, and the pip
 * that says which way it is looking. Both are marks on the ground at a figure's
 * feet, and both used to carry their own copy of these four numbers — which is
 * how the pip ends up floating beside the ring the first time either moves.
 */
export const UNIT_FOOTING = { cx: 16, cy: 29.5, rx: 11, ry: 2.25 } as const;

export function groundShadow(
  at: { cx: number; cy: number },
  size: { rx: number; ry: number },
  weight: ShadowWeight = 'normal',
): string {
  return `<ellipse cx="${at.cx}" cy="${at.cy}" rx="${size.rx}" ry="${size.ry}"`
    + ` fill="${PAL.ink}" opacity="${WEIGHT_ALPHA[weight]}"/>`;
}
