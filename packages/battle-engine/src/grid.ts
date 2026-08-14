import type { Coord, GameMap, TerrainId } from './types';

export const idx = (map: { width: number }, x: number, y: number): number => y * map.width + x;
export const coordOf = (map: { width: number }, i: number): Coord => ({
  x: i % map.width,
  y: Math.floor(i / map.width),
});

export const inBounds = (map: { width: number; height: number }, x: number, y: number): boolean =>
  x >= 0 && y >= 0 && x < map.width && y < map.height;

export const sameCoord = (a: Coord, b: Coord): boolean => a.x === b.x && a.y === b.y;

export const terrainAt = (map: GameMap, c: Coord): TerrainId => map.tiles[idx(map, c.x, c.y)];
/**
 * Row-major storage helpers.
 *
 * What is left here is about *storage*: where a cell sits in a flat array and
 * whether it is on the board at all. How far apart two cells are, what counts as
 * next to what and which way a unit can face moved to `TacticalGrid`, because
 * those are the answers a tiling gives — and this module had four-directional
 * ones baked in as free functions fifteen modules imported.
 */

/**
 * Do these two cells share a boundary in storage?
 *
 * The question a cliff edge is defined by, and it was asked in four places with
 * the same hand-written Manhattan expression: the layer writer, the map builder
 * and the level linter twice over. Storage adjacency, deliberately not the
 * tiling's: a cliff is cut between rows and columns of the file, whatever the
 * tiling then makes of them.
 */
export const sharesEdge = (a: Coord, b: Coord): boolean =>
  Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;

/**
 * Identity of the boundary between two cells, from either side.
 *
 * Pure coordinate geometry, so it lives with the rest of it: while it sat in the
 * module that also answers flanking and facing — which need units and teams —
 * the map's layer writer could not use it without importing the battle state.
 */
export function edgeKey(a: Coord, b: Coord): string {
  return [`${a.x},${a.y}`, `${b.x},${b.y}`].sort().join('|');
}

/** Integer Bresenham trace including both endpoints. */
export function lineBetween(from: Coord, to: Coord): Coord[] {
  const out: Coord[] = [];
  let x = from.x;
  let y = from.y;
  const dx = Math.abs(to.x - from.x);
  const dy = Math.abs(to.y - from.y);
  const sx = from.x < to.x ? 1 : -1;
  const sy = from.y < to.y ? 1 : -1;
  let error = dx - dy;
  for (;;) {
    out.push({ x, y });
    if (x === to.x && y === to.y) return out;
    const twice = error * 2;
    if (twice > -dy) {
      error -= dy;
      x += sx;
    }
    if (twice < dx) {
      error += dx;
      y += sy;
    }
  }
}

/** Stable per-tile pseudo-random value in [0,1) — used for art variation. */
export function tileHash(x: number, y: number, salt = 0): number {
  let h = (x * 374761393 + y * 668265263 + salt * 2246822519) | 0;
  h = (h ^ (h >>> 13)) * 1274126177;
  h = h ^ (h >>> 16);
  return ((h >>> 0) % 100000) / 100000;
}

