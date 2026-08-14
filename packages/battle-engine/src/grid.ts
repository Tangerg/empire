import type { Coord, GameMap, TerrainId, PlayerId } from './types';

export const idx = (map: { width: number }, x: number, y: number): number => y * map.width + x;
export const coordOf = (map: { width: number }, i: number): Coord => ({
  x: i % map.width,
  y: Math.floor(i / map.width),
});

export const inBounds = (map: { width: number; height: number }, x: number, y: number): boolean =>
  x >= 0 && y >= 0 && x < map.width && y < map.height;

export const sameCoord = (a: Coord, b: Coord): boolean => a.x === b.x && a.y === b.y;

/** Manhattan distance — the grid is 4-connected, like Ancient Empires. */
export const dist = (a: Coord, b: Coord): number => Math.abs(a.x - b.x) + Math.abs(a.y - b.y);

export const NEIGHBOURS: readonly Coord[] = [
  { x: 0, y: -1 },
  { x: 1, y: 0 },
  { x: 0, y: 1 },
  { x: -1, y: 0 },
];

export function neighbours(map: { width: number; height: number }, c: Coord): Coord[] {
  const out: Coord[] = [];
  for (const d of NEIGHBOURS) {
    const x = c.x + d.x;
    const y = c.y + d.y;
    if (inBounds(map, x, y)) out.push({ x, y });
  }
  return out;
}

/** Every tile whose Manhattan distance from `c` is within [min, max]. */
export function ring(
  map: { width: number; height: number },
  c: Coord,
  min: number,
  max: number,
): Coord[] {
  const out: Coord[] = [];
  for (let dy = -max; dy <= max; dy++) {
    const room = max - Math.abs(dy);
    for (let dx = -room; dx <= room; dx++) {
      const d = Math.abs(dx) + Math.abs(dy);
      if (d < min || d > max) continue;
      const x = c.x + dx;
      const y = c.y + dy;
      if (inBounds(map, x, y)) out.push({ x, y });
    }
  }
  return out;
}

export const terrainAt = (map: GameMap, c: Coord): TerrainId => map.tiles[idx(map, c.x, c.y)];
export const ownerAt = (map: GameMap, c: Coord): PlayerId => map.owners[idx(map, c.x, c.y)];

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

/** Distance to the closest of several places — not the place itself. */
export function nearestDistance(from: Coord, places: readonly Coord[]): number {
  let best = Infinity;
  for (const place of places) best = Math.min(best, dist(from, place));
  return best;
}
