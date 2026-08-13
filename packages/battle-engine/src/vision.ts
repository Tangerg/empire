import { Battlefield } from './domain/battlefield';
import { dist, idx, ring } from './grid';
import { areAllies } from './state';
import type { GameState, PlayerId, Unit } from './types';
import { type ContentCatalog } from './content-pack';

/**
 * Fog of war (off by default, per-level flag). Terrain is always known — only
 * units are hidden — and a unit sitting in cover (`opaque` terrain) is spotted
 * only from an adjacent tile. Simple, readable, and enough to make scouting
 * matter for future modes.
 */
export function visibleTiles(state: GameState, viewer: PlayerId, content: ContentCatalog): Set<number> {
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
    for (const c of ring(state.map, { x: u.x, y: u.y }, 0, def.vision + bonus)) {
      seen.add(idx(state.map, c.x, c.y));
    }
  }
  for (let i = 0; i < state.map.owners.length; i++) {
    if (state.map.owners[i] === viewer) seen.add(i);
  }
  return seen;
}

export function isUnitVisible(
  content: ContentCatalog,
  state: GameState,
  viewer: PlayerId,
  u: Unit,
  seen?: Set<number>,
): boolean {
  if (!state.rules.fog) return true;
  if (u.owner === viewer || areAllies(state, u.owner, viewer)) return true;
  const i = idx(state.map, u.x, u.y);
  const visible = seen ?? visibleTiles(state, viewer, content);
  if (!visible.has(i)) return false;
  if (!new Battlefield(state, content).cell(u).terrain.opaque) return true;
  // Hidden in cover unless something friendly is standing right next to it.
  return state.units.some(
    (o) =>
      (o.owner === viewer || areAllies(state, o.owner, viewer)) &&
      dist({ x: o.x, y: o.y }, { x: u.x, y: u.y }) === 1,
  );
}

export function visibleUnits(state: GameState, viewer: PlayerId, content: ContentCatalog): Unit[] {
  const seen = visibleTiles(state, viewer, content);
  return state.units.filter((u) => isUnitVisible(content, state, viewer, u, seen));
}
