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

/** Stable per-tile pseudo-random value in [0,1) — used for art variation. */
export function tileHash(x: number, y: number, salt = 0): number {
  let h = (x * 374761393 + y * 668265263 + salt * 2246822519) | 0;
  h = (h ^ (h >>> 13)) * 1274126177;
  h = h ^ (h >>> 16);
  return ((h >>> 0) % 100000) / 100000;
}
