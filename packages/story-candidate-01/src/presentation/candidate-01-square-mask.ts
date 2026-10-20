import { TILE } from '@empire/game-ui';

/**
 * The eight cells a blob sheet counts, in the bit order the kit numbers them.
 *
 * `["N","NE","E","SE","S","SW","W","NW"]`, which every transition atlas in the
 * manifest declares as its `bitOrder`. These are the square tiling's neighbours,
 * written out rather than asked of the board — see `candidate01PaintsCells`, which
 * is where this pack says out loud that its sheets only fit a square field.
 */
const NEIGHBOURS = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]] as const;

/**
 * The eight-neighbour mask a blob transition sheet is indexed by.
 *
 * One of these, taking the question. There were two — one asking whether a
 * neighbour blends with the same sheet, one asking whether it is a river bank —
 * and they were the same eight shifts written out twice, which is one more place
 * for a bit to be in the wrong position than the kit's `bitOrder` deserves.
 */
export const candidate01BlobMask = (x: number, y: number, joins: (x: number, y: number) => boolean): number =>
  NEIGHBOURS.reduce(
    (mask, [dx, dy], bit) => (joins(x + dx, y + dy) ? mask | (1 << bit) : mask),
    0,
  );

/**
 * One geometric union, not separately antialiased cell clips. Equal winding
 * cancels shared edges at fractional camera scales without expanding permission.
 */
export function candidate01CellUnionMarkup(
  cells: readonly (readonly [number, number])[],
  originX = 0,
  originY = 0,
): string {
  const path = cells.map(([x, y]) =>
    `M${x * TILE - originX} ${y * TILE - originY}h${TILE}v${TILE}h${-TILE}Z`).join('');
  return `<path d="${path}" fill-rule="nonzero" clip-rule="nonzero"/>`;
}

