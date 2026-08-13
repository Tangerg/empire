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
export function visibleTiles(s: GameState, viewer: PlayerId, content: ContentCatalog): Set<number> {
  const seen = new Set<number>();
  const battlefield = new Battlefield(s, content);
  if (!s.rules.fog) {
    for (let i = 0; i < s.map.tiles.length; i++) seen.add(i);
    return seen;
  }
  for (const u of s.units) {
    if (!areAllies(s, u.owner, viewer) && u.owner !== viewer) continue;
    const def = content.units.get(u.type);
    const bonus = battlefield.cell(u).vision;
    for (const c of ring(s.map, { x: u.x, y: u.y }, 0, def.vision + bonus)) {
      seen.add(idx(s.map, c.x, c.y));
    }
  }
  for (let i = 0; i < s.map.owners.length; i++) {
    if (s.map.owners[i] === viewer) seen.add(i);
  }
  return seen;
}

export function isUnitVisible(
  content: ContentCatalog,
  s: GameState,
  viewer: PlayerId,
  u: Unit,
  seen?: Set<number>,
): boolean {
  if (!s.rules.fog) return true;
  if (u.owner === viewer || areAllies(s, u.owner, viewer)) return true;
  const i = idx(s.map, u.x, u.y);
  const visible = seen ?? visibleTiles(s, viewer, content);
  if (!visible.has(i)) return false;
  if (!new Battlefield(s, content).cell(u).terrain.opaque) return true;
  // Hidden in cover unless something friendly is standing right next to it.
  return s.units.some(
    (o) =>
      (o.owner === viewer || areAllies(s, o.owner, viewer)) &&
      dist({ x: o.x, y: o.y }, { x: u.x, y: u.y }) === 1,
  );
}

export function visibleUnits(s: GameState, viewer: PlayerId, content: ContentCatalog): Unit[] {
  const seen = visibleTiles(s, viewer, content);
  return s.units.filter((u) => isUnitVisible(content, s, viewer, u, seen));
}
