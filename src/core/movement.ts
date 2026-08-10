import { Terrains } from './data/terrain';
import { unitDef } from './data/units';
import { idx, inBounds, NEIGHBOURS, ring } from './grid';
import { areAllies, areEnemies, unitAt } from './state';
import type { Coord, GameState, Unit } from './types';

export interface ReachableTile {
  index: number;
  x: number;
  y: number;
  cost: number;
  /** Previous tile index on the cheapest path, -1 for the origin. */
  from: number;
  /** False when another unit is standing there (can pass, cannot stop). */
  free: boolean;
}

export interface MoveField {
  origin: Coord;
  unit: number;
  tiles: Map<number, ReachableTile>;
  /** Indices the unit may actually finish its move on. */
  stops: Set<number>;
}

/**
 * Dijkstra over entry costs. Allies can be passed through (configurable), enemy
 * units block entirely, and only empty tiles are valid stopping points.
 */
export function computeMoveField(s: GameState, unit: Unit): MoveField {
  const def = unitDef(unit.type);
  const map = s.map;
  const budget = def.movement;
  const start = idx(map, unit.x, unit.y);

  const tiles = new Map<number, ReachableTile>();
  tiles.set(start, { index: start, x: unit.x, y: unit.y, cost: 0, from: -1, free: true });

  // Small maps + small budgets: a bucket queue keyed by cost is plenty.
  const buckets: number[][] = Array.from({ length: budget + 1 }, () => []);
  buckets[0].push(start);

  for (let c = 0; c <= budget; c++) {
    for (let qi = 0; qi < buckets[c].length; qi++) {
      const cur = buckets[c][qi];
      const node = tiles.get(cur)!;
      if (node.cost !== c) continue; // stale entry

      const cx = cur % map.width;
      const cy = Math.floor(cur / map.width);
      for (const d of NEIGHBOURS) {
        const nx = cx + d.x;
        const ny = cy + d.y;
        if (!inBounds(map, nx, ny)) continue;
        const ni = idx(map, nx, ny);

        const terrain = Terrains.get(map.tiles[ni]);
        const step = terrain.cost[def.movementClass];
        if (step === null) continue;

        const blocker = unitAt(s, nx, ny);
        if (blocker && blocker.id !== unit.id) {
          if (areEnemies(s, blocker.owner, unit.owner)) {
            if (s.rules.enemiesBlockMovement) continue;
          } else if (!s.rules.friendlyPassThrough) {
            continue;
          }
        }

        const nc = c + step;
        if (nc > budget) continue;
        const prev = tiles.get(ni);
        if (prev && prev.cost <= nc) continue;
        tiles.set(ni, {
          index: ni,
          x: nx,
          y: ny,
          cost: nc,
          from: cur,
          free: !blocker || blocker.id === unit.id,
        });
        buckets[nc].push(ni);
      }
    }
  }

  const stops = new Set<number>();
  for (const t of tiles.values()) if (t.free) stops.add(t.index);

  return { origin: { x: unit.x, y: unit.y }, unit: unit.id, tiles, stops };
}

/** Cheapest path from the field's origin to `to`, inclusive of both ends. */
export function pathTo(field: MoveField, map: { width: number }, to: Coord): Coord[] | null {
  const target = to.y * map.width + to.x;
  if (!field.tiles.has(target)) return null;
  const path: Coord[] = [];
  let cur = target;
  const guard = field.tiles.size + 2;
  for (let n = 0; n <= guard; n++) {
    const node = field.tiles.get(cur);
    if (!node) return null;
    path.push({ x: node.x, y: node.y });
    if (node.from === -1) {
      path.reverse();
      return path;
    }
    cur = node.from;
  }
  return null;
}

export function moveCostOf(field: MoveField, map: { width: number }, to: Coord): number | null {
  return field.tiles.get(to.y * map.width + to.x)?.cost ?? null;
}

/** Tiles this unit could attack if it stood on `from`. */
export function attackTilesFrom(s: GameState, unit: Unit, from: Coord): Coord[] {
  const def = unitDef(unit.type);
  if (def.maxRange <= 0) return [];
  return ring(s.map, from, def.minRange, def.maxRange);
}

/**
 * Union of every tile the unit could strike this turn: from its current tile
 * always, and from each reachable stop when the unit may attack after moving.
 * Drives the "enemy threat range" overlay.
 */
export function threatTiles(s: GameState, unit: Unit, field?: MoveField): Set<number> {
  const def = unitDef(unit.type);
  const out = new Set<number>();
  const add = (c: Coord) => out.add(idx(s.map, c.x, c.y));

  for (const c of attackTilesFrom(s, unit, { x: unit.x, y: unit.y })) add(c);
  if (!def.attackAfterMove) return out;

  const f = field ?? computeMoveField(s, unit);
  for (const i of f.stops) {
    const from = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
    for (const c of attackTilesFrom(s, unit, from)) add(c);
  }
  return out;
}

/** Enemy units in range if `unit` stood on `from`. */
export function targetsFrom(s: GameState, unit: Unit, from: Coord): Unit[] {
  const out: Unit[] = [];
  for (const c of attackTilesFrom(s, unit, from)) {
    const other = unitAt(s, c.x, c.y);
    if (other && areEnemies(s, other.owner, unit.owner)) out.push(other);
  }
  return out;
}

/** Wounded allies adjacent to `from` (cleric targeting). */
export function healTargetsFrom(s: GameState, unit: Unit, from: Coord): Unit[] {
  const out: Unit[] = [];
  for (const c of ring(s.map, from, 1, 1)) {
    const other = unitAt(s, c.x, c.y);
    if (!other || other.id === unit.id) continue;
    if (!areAllies(s, other.owner, unit.owner)) continue;
    if (other.hp >= unitDef(other.type).maxHp) continue;
    out.push(other);
  }
  return out;
}
