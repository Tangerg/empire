import { Battlefield } from './domain/battlefield';
import { boardOf } from './domain/board';
import { idx } from './grid';
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
import type { GridRules } from './tactical-grid';

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

/**
 * Ports declared by this module. `BattleRuleServices` satisfies them
 * structurally, so every caller that already holds the ruleset is unchanged.
 */
export interface MovementRules extends GridRules {
  readonly content: ContentCatalog;
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
export function computeMoveField(rules: MovementRules, state: GameState, unit: Unit): MoveField {
  const content = rules.content;
  const def = content.units.get(unit.type);
  const map = state.map;
  const board = boardOf(rules, state);
  const battlefield = new Battlefield(state, content);
  const budget = Math.max(
    0,
    def.movement + combinedStatusModifiers(content, unit).movementDelta +
      commanderAuraFor(rules, state, unit).movementDelta + formationMovementDelta(rules, state, unit),
  );
  const start = idx(map, unit.x, unit.y);
  // Ground the enemy holds. A unit may step into it and may leave the tile it
  // started on, but it may not walk on through — which is what makes a battle
  // line a line instead of a suggestion.
  const controlled = hostileControlZone(rules, state, unit);

  const tiles = new Map<number, ReachableTile>();
  tiles.set(start, { index: start, x: unit.x, y: unit.y, cost: 0, from: -1, free: true });

  // Small maps + small budgets: a bucket queue keyed by cost is plenty.
  const buckets: number[][] = Array.from({ length: budget + 1 }, () => []);
  buckets[0].push(start);

  for (let c = 0; c <= budget; c++) {
    for (let qi = 0; qi < buckets[c].length; qi++) {
      const node = tiles.get(buckets[c][qi]);
      // A tile only enters a bucket once it has a node, and a cheaper route
      // found later leaves the older entry behind at its old cost.
      if (!node || node.cost !== c) continue;
      const cur = node.index;

      const from = board.coordOf(cur);
      for (const next of board.neighbours(from)) {
        const nx = next.x;
        const ny = next.y;
        const ni = board.indexOf(next);

        const step = battlefield.traversalCost(from, next, def.movementClass);
        if (step == null) continue;

        const blocker = unitAt(state, { x: nx, y: ny });
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
  const target = idx(map, to.x, to.y);
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
  return field.tiles.get(idx(map, to.x, to.y))?.cost ?? null;
}

/** Tiles this unit could attack if it stood on `from`. */
export function attackTilesFrom(
  rules: MovementRules,
  state: GameState,
  unit: Unit,
  from: Coord,
  weapon?: WeaponDef,
): Coord[] {
  const resolved = weapon ?? primaryWeapon(rules.content, unit);
  if (resolved.maxRange <= 0) return [];
  return boardOf(rules, state).ring(from, resolved.minRange, resolved.maxRange).filter(
    (target) => resolved.lineOfSight !== 'direct' || hasDirectLineOfSight(rules, state, from, target),
  );
}

export function hasDirectLineOfSight(
  rules: MovementRules,
  state: GameState,
  from: Coord,
  target: Coord,
): boolean {
  const content = rules.content;
  const trace = boardOf(rules, state).line(from, target);
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
  rules: MovementRules,
  state: GameState,
  unit: Unit,
  field?: MoveField,
): Set<number> {
  const out = new Set<number>();
  const add = (at: Coord) => out.add(idx(state.map, at.x, at.y));

  const weapons = unitWeapons(rules.content, unit);
  for (const weapon of weapons) {
    for (const at of attackTilesFrom(rules, state, unit, { x: unit.x, y: unit.y }, weapon)) add(at);
  }

  const f = field ?? computeMoveField(rules, state, unit);
  for (const weapon of weapons.filter((candidate) => candidate.moveAndAttack)) {
    for (const i of f.stops) {
      const from = { x: i % state.map.width, y: Math.floor(i / state.map.width) };
      for (const c of attackTilesFrom(rules, state, unit, from, weapon)) add(c);
    }
  }
  return out;
}

/** Unit and destructible-structure targets share the same range query. */
export function attackTargetCoords(
  rules: MovementRules,
  state: GameState,
  unit: Unit,
  from: Coord,
  weapon?: WeaponDef,
): Coord[] {
  const content = rules.content;
  const out: Coord[] = [];
  for (const cell of attackTilesFrom(rules, state, unit, from, weapon)) {
    const other = unitAt(state, { x: cell.x, y: cell.y });
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
export function healTargetsFrom(rules: MovementRules, state: GameState, unit: Unit, from: Coord): Unit[] {
  const content = rules.content;
  const found: Unit[] = [];
  for (const c of boardOf(rules, state).ring(from, 1, 1)) {
    const other = unitAt(state, { x: c.x, y: c.y });
    if (!other || other.id === unit.id) continue;
    if (!areAllies(state, other.owner, unit.owner)) continue;
    if (other.hp >= content.units.get(other.type).maxHp) continue;
    found.push(other);
  }
  return found;
}
