import { Battlefield } from './domain/battlefield';
import { boardOf } from './domain/board';
import { idx } from './grid';
import { areAllies } from './state';
import type { GameState, PlayerId, Unit } from './types';
import { type ContentCatalog } from './content-pack';
import type { GridRules } from './tactical-grid';

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface VisionRules extends GridRules {
  readonly content: ContentCatalog;
}

/**
 * Fog of war (off by default, per-level flag). Terrain is always known — only
 * units are hidden — and a unit sitting in cover (`opaque` terrain) is spotted
 * only from an adjacent tile. Simple, readable, and enough to make scouting
 * matter for future modes.
 */
export function visibleTiles(rules: VisionRules, state: GameState, viewer: PlayerId): Set<number> {
  const content = rules.content;
  const seen = new Set<number>();
  const battlefield = new Battlefield(state, content);
  if (!state.rules.fog) {
    for (let i = 0; i < state.map.tiles.length; i++) seen.add(i);
    return seen;
  }
  for (const u of state.units) {
    if (!areAllies(state, u.owner, viewer) && u.owner !== viewer) continue;
    const def = content.units.get(u.type);
    const bonus = battlefield.cell(u).vision;
    for (const index of boardOf(rules, state).ringIndices({ x: u.x, y: u.y }, 0, def.vision + bonus)) {
      seen.add(index);
    }
  }
  for (let i = 0; i < state.map.owners.length; i++) {
    if (state.map.owners[i] === viewer) seen.add(i);
  }
  return seen;
}

export function isUnitVisible(
  rules: VisionRules,
  state: GameState,
  viewer: PlayerId,
  u: Unit,
  seen?: Set<number>,
): boolean {
  if (!state.rules.fog) return true;
  if (u.owner === viewer || areAllies(state, u.owner, viewer)) return true;
  const i = idx(state.map, u.x, u.y);
  const visible = seen ?? visibleTiles(rules, state, viewer);
  if (!visible.has(i)) return false;
  if (!new Battlefield(state, rules.content).cell(u).terrain.opaque) return true;
  // Hidden in cover unless something friendly is standing right next to it.
  const board = boardOf(rules, state);
  return state.units.some(
    (o) =>
      (o.owner === viewer || areAllies(state, o.owner, viewer)) &&
      board.distance({ x: o.x, y: o.y }, { x: u.x, y: u.y }) === 1,
  );
}

export function visibleUnits(rules: VisionRules, state: GameState, viewer: PlayerId): Unit[] {
  const seen = visibleTiles(rules, state, viewer);
  return state.units.filter((u) => isUnitVisible(rules, state, viewer, u, seen));
}
