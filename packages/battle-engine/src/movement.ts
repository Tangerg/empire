import { Battlefield } from './domain/battlefield';
import { idx, inBounds, lineBetween, NEIGHBOURS, ring } from './grid';
import { areAllies, areEnemies, unitAt } from './state';
import { combinedStatusModifiers } from './statuses';
import { structureAt } from './structures';
import { primaryWeapon, unitWeapons } from './combat';
import { commanderAuraFor } from './commanders';
import { formationMovementDelta } from './formations';
import { hostileControlZone } from './zone-of-control';
import type { WeaponDef } from './types';
import type { Coord, GameState, Unit } from './types';
import { type ContentCatalog } from './content-pack';

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
export function computeMoveField(content: ContentCatalog, state: GameState, unit: Unit): MoveField {
  const def = content.units.get(unit.type);
  const map = state.map;
  const battlefield = new Battlefield(state, content);
  const budget = Math.max(
    0,
    def.movement + combinedStatusModifiers(unit, content).movementDelta +
      commanderAuraFor(state, unit).movementDelta + formationMovementDelta(state, unit, content),
  );
  const start = idx(map, unit.x, unit.y);
  // Ground the enemy holds. A unit may step into it and may leave the tile it
  // started on, but it may not walk on through — which is what makes a battle
  // line a line instead of a suggestion.
  const controlled = hostileControlZone(content, state, unit);

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

        const blocker = unitAt(state, nx, ny);
        if (blocker && blocker.id !== unit.id) {
          if (areEnemies(state, blocker.owner, unit.owner)) {
            if (state.rules.enemiesBlockMovement) continue;
          } else if (!state.rules.friendlyPassThrough) {
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
        if (!controlled.has(ni)) buckets[nc].push(ni);
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
  content: ContentCatalog,
  state: GameState,
  unit: Unit,
  from: Coord,
  weapon?: WeaponDef,
): Coord[] {
  const resolved = weapon ?? primaryWeapon(unit, content);
  if (resolved.maxRange <= 0) return [];
  return ring(state.map, from, resolved.minRange, resolved.maxRange).filter(
    (target) => resolved.lineOfSight !== 'direct' || hasDirectLineOfSight(content, state, from, target),
  );
}

export function hasDirectLineOfSight(
  content: ContentCatalog,
  state: GameState,
  from: Coord,
  target: Coord,
): boolean {
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
export function threatTiles(
  content: ContentCatalog,
  state: GameState,
  unit: Unit,
  field?: MoveField,
): Set<number> {
  const out = new Set<number>();
  const add = (at: Coord) => out.add(idx(state.map, at.x, at.y));

  const weapons = unitWeapons(unit, content);
  for (const weapon of weapons) {
    for (const at of attackTilesFrom(content, state, unit, { x: unit.x, y: unit.y }, weapon)) add(at);
  }

  const f = field ?? computeMoveField(content, state, unit);
  for (const weapon of weapons.filter((candidate) => candidate.moveAndAttack)) {
    for (const i of f.stops) {
      const from = { x: i % state.map.width, y: Math.floor(i / state.map.width) };
      for (const c of attackTilesFrom(content, state, unit, from, weapon)) add(c);
    }
  }
  return out;
}

/** Enemy units in range if `unit` stood on `from`. */
export function targetsFrom(
  content: ContentCatalog,
  state: GameState,
  unit: Unit,
  from: Coord,
  weapon?: WeaponDef,
): Unit[] {
  const out: Unit[] = [];
  for (const c of attackTilesFrom(content, state, unit, from, weapon)) {
    const other = unitAt(state, c.x, c.y);
    if (other && areEnemies(state, other.owner, unit.owner)) out.push(other);
  }
  return out;
}

/** Unit and destructible-structure targets share the same range query. */
export function attackTargetCoords(
  content: ContentCatalog,
  state: GameState,
  unit: Unit,
  from: Coord,
  weapon?: WeaponDef,
): Coord[] {
  const out: Coord[] = [];
  for (const cell of attackTilesFrom(content, state, unit, from, weapon)) {
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
export function healTargetsFrom(content: ContentCatalog, state: GameState, unit: Unit, from: Coord): Unit[] {
  const out: Unit[] = [];
  for (const c of ring(state.map, from, 1, 1)) {
    const other = unitAt(state, c.x, c.y);
    if (!other || other.id === unit.id) continue;
    if (!areAllies(state, other.owner, unit.owner)) continue;
    if (other.hp >= content.units.get(other.type).maxHp) continue;
    out.push(other);
  }
  return out;
}
