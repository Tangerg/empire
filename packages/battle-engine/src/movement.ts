import { Battlefield } from './domain/battlefield';
import { idx, inBounds, lineBetween, NEIGHBOURS, ring } from './grid';
import { areAllies, areEnemies, unitAt } from './state';
import { combinedStatusModifiers } from './statuses';
import { structureAt } from './structures';
import { primaryWeapon, unitWeapons } from './combat';
import { commanderAuraFor } from './commanders';
import { formationMovementDelta } from './formations';
import type { WeaponDef } from './types';
import type { Coord, GameState, Unit } from './types';
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';

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
export function computeMoveField(s: GameState, unit: Unit, content: ContentCatalog = GlobalContentCatalog): MoveField {
  const def = content.units.get(unit.type);
  const map = s.map;
  const battlefield = new Battlefield(s, content);
  const budget = Math.max(
    0,
    def.movement + combinedStatusModifiers(unit, content).movementDelta +
      commanderAuraFor(s, unit).movementDelta + formationMovementDelta(s, unit, content),
  );
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

        const cell = battlefield.cellAt(nx, ny);
        const step = battlefield.traversalCost({ x: cx, y: cy }, { x: nx, y: ny }, def.movementClass);
        if (step == null) continue;
        if (cell.blocksMovement) continue;

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
export function attackTilesFrom(
  s: GameState,
  unit: Unit,
  from: Coord,
  weapon: WeaponDef | undefined = undefined,
  content: ContentCatalog = GlobalContentCatalog,
): Coord[] {
  const resolved = weapon ?? primaryWeapon(unit, content);
  if (resolved.maxRange <= 0) return [];
  return ring(s.map, from, resolved.minRange, resolved.maxRange).filter(
    (target) => resolved.lineOfSight !== 'direct' || hasDirectLineOfSight(s, from, target, content),
  );
}

export function hasDirectLineOfSight(state: GameState, from: Coord, target: Coord, content: ContentCatalog = GlobalContentCatalog): boolean {
  const trace = lineBetween(from, target);
  const battlefield = new Battlefield(state, content);
  const fromEye = battlefield.cell(from).elevation + 1;
  const targetEye = battlefield.cell(target).elevation + 1;
  for (let i = 1; i < trace.length - 1; i++) {
    const cell = battlefield.cell(trace[i]);
    if (!cell.blocksVision) continue;
    const progress = i / (trace.length - 1);
    const rayHeight = fromEye + (targetEye - fromEye) * progress;
    if (cell.obstructionTop >= rayHeight) return false;
  }
  return true;
}

/**
 * Union of every tile the unit could strike this turn: from its current tile
 * always, and from each reachable stop when the unit may attack after moving.
 * Drives the "enemy threat range" overlay.
 */
export function threatTiles(s: GameState, unit: Unit, field?: MoveField, content: ContentCatalog = GlobalContentCatalog): Set<number> {
  const out = new Set<number>();
  const add = (c: Coord) => out.add(idx(s.map, c.x, c.y));

  const weapons = unitWeapons(unit, content);
  for (const weapon of weapons) {
    for (const c of attackTilesFrom(s, unit, { x: unit.x, y: unit.y }, weapon, content)) add(c);
  }

  const f = field ?? computeMoveField(s, unit, content);
  for (const weapon of weapons.filter((candidate) => candidate.moveAndAttack)) {
    for (const i of f.stops) {
      const from = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
      for (const c of attackTilesFrom(s, unit, from, weapon, content)) add(c);
    }
  }
  return out;
}

/** Enemy units in range if `unit` stood on `from`. */
export function targetsFrom(
  s: GameState,
  unit: Unit,
  from: Coord,
  weapon: WeaponDef | undefined = undefined,
  content: ContentCatalog = GlobalContentCatalog,
): Unit[] {
  const out: Unit[] = [];
  for (const c of attackTilesFrom(s, unit, from, weapon, content)) {
    const other = unitAt(s, c.x, c.y);
    if (other && areEnemies(s, other.owner, unit.owner)) out.push(other);
  }
  return out;
}

/** Unit and destructible-structure targets share the same range query. */
export function attackTargetCoords(
  state: GameState,
  unit: Unit,
  from: Coord,
  weapon: WeaponDef | undefined = undefined,
  content: ContentCatalog = GlobalContentCatalog,
): Coord[] {
  const out: Coord[] = [];
  for (const cell of attackTilesFrom(state, unit, from, weapon, content)) {
    const other = unitAt(state, cell.x, cell.y);
    if (other && areEnemies(state, other.owner, unit.owner)) {
      out.push(cell);
      continue;
    }
    const structure = structureAt(state, cell.x, cell.y);
    if (!structure || !content.structures.get(structure.type).targetable) continue;
    if (structure.owner === 0 || areEnemies(state, structure.owner, unit.owner)) out.push(cell);
  }
  return out;
}

/** Wounded allies adjacent to `from` (support-healing targeting). */
export function healTargetsFrom(s: GameState, unit: Unit, from: Coord, content: ContentCatalog = GlobalContentCatalog): Unit[] {
  const out: Unit[] = [];
  for (const c of ring(s.map, from, 1, 1)) {
    const other = unitAt(s, c.x, c.y);
    if (!other || other.id === unit.id) continue;
    if (!areAllies(s, other.owner, unit.owner)) continue;
    if (other.hp >= content.units.get(other.type).maxHp) continue;
    out.push(other);
  }
  return out;
}
