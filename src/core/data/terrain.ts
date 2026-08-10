import { Registry } from '../registry';
import type { MoveCosts, TerrainDef } from '../types';

export const Terrains = new Registry<TerrainDef>('terrain');

const costs = (
  foot: number | null,
  mounted: number | null,
  heavy: number | null,
  flying: number | null,
): MoveCosts => ({ foot, mounted, heavy, flying });

const IMPASSABLE = costs(null, null, null, null);

function terrain(def: Partial<TerrainDef> & { id: string; name: string; cost: MoveCosts }): TerrainDef {
  return {
    defense: 0,
    vision: 0,
    opaque: false,
    capturable: false,
    income: 0,
    heal: 0,
    produces: [],
    hq: false,
    tags: [],
    ...def,
  };
}

/**
 * Terrain table. `char` (see mapio.ts) keeps the on-disk level format readable.
 *
 * Movement classes: foot / mounted / heavy / flying.
 * Defense is a straight damage reduction, exactly like Ancient Empires' terrain
 * bonus — the forecast in the HUD is therefore exact.
 */
Terrains.defineAll([
  terrain({ id: 'plain', name: '平原', cost: costs(1, 1, 1, 1), defense: 0.0 }),
  terrain({ id: 'road', name: '道路', cost: costs(1, 1, 1, 1), defense: 0.0, tags: ['road'] }),
  terrain({ id: 'bridge', name: '桥梁', cost: costs(1, 1, 1, 1), defense: 0.0, tags: ['road'] }),
  terrain({
    id: 'forest',
    name: '森林',
    cost: costs(2, 3, 3, 1),
    defense: 0.2,
    opaque: true,
    tags: ['rough'],
  }),
  terrain({
    id: 'hill',
    name: '丘陵',
    cost: costs(2, 3, 3, 1),
    defense: 0.3,
    vision: 1,
    tags: ['rough', 'high'],
  }),
  terrain({
    id: 'mountain',
    name: '山地',
    cost: costs(3, null, null, 1),
    defense: 0.4,
    vision: 2,
    opaque: true,
    tags: ['rough', 'high'],
  }),
  terrain({ id: 'water', name: '水域', cost: costs(null, null, null, 1), tags: ['water'] }),
  terrain({ id: 'wall', name: '城墙', cost: IMPASSABLE, opaque: true, tags: ['blocking'] }),
  terrain({
    id: 'village',
    name: '村庄',
    cost: costs(1, 1, 1, 1),
    defense: 0.15,
    capturable: true,
    income: 100,
    heal: 20,
    tags: ['building'],
  }),
  terrain({
    id: 'barracks',
    name: '兵营',
    cost: costs(1, 1, 1, 1),
    defense: 0.2,
    capturable: true,
    income: 50,
    heal: 20,
    produces: ['soldier', 'archer', 'rogue', 'cleric'],
    tags: ['building', 'production'],
  }),
  terrain({
    id: 'castle',
    name: '城堡',
    cost: costs(1, 1, 1, 1),
    defense: 0.3,
    capturable: true,
    income: 100,
    heal: 30,
    hq: true,
    produces: ['soldier', 'archer', 'rogue', 'cleric', 'mage', 'knight', 'ogre', 'ballista', 'dragon'],
    tags: ['building', 'production', 'hq'],
  }),
]);

export const terrainDef = (id: string): TerrainDef => Terrains.get(id);
