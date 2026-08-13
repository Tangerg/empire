import { defineTerrain, FUNDS_RESOURCE, type MoveCosts, type TerrainDef, type TerrainId } from '@empire/battle-engine';

const costs = (
  foot: number | null,
  mounted: number | null,
  heavy: number | null,
  flying: number | null,
  naval: number | null = null,
  amphibious: number | null = foot,
): MoveCosts => ({ foot, mounted, heavy, flying, naval, amphibious });

const impassable = costs(null, null, null, null);

export const ANCIENT_EMPIRES_TERRAINS: readonly TerrainDef[] = [
  defineTerrain({ id: 'plain', name: '平原', cost: costs(1, 1, 1, 1) }),
  defineTerrain({ id: 'road', name: '道路', cost: costs(1, 1, 1, 1), tags: ['road'] }),
  defineTerrain({ id: 'bridge', name: '桥梁', cost: costs(1, 1, 1, 1), tags: ['road'] }),
  defineTerrain({
    id: 'forest', name: '森林', cost: costs(2, 3, 3, 1), defense: 0.2, cover: 'half', obstructionHeight: 2, opaque: true, tags: ['rough'],
  }),
  defineTerrain({
    id: 'hill', name: '丘陵', cost: costs(2, 3, 3, 1), defense: 0.3, vision: 1, tags: ['rough', 'high'],
  }),
  defineTerrain({
    id: 'mountain', name: '山地', cost: costs(3, null, null, 1), defense: 0.4, cover: 'half', obstructionHeight: 2, vision: 2, opaque: true, tags: ['rough', 'high'],
  }),
  defineTerrain({ id: 'water', name: '水域', cost: costs(null, null, null, 1, 1, 1), tags: ['water'] }),
  defineTerrain({ id: 'wall', name: '城墙', cost: impassable, cover: 'full', obstructionHeight: 3, opaque: true, tags: ['blocking'] }),
  defineTerrain({
    id: 'village', name: '村庄', cost: costs(1, 1, 1, 1), defense: 0.15, capturable: true,
    ownerTurnGrants: [{ resource: FUNDS_RESOURCE, amount: 100 }], heal: 20, tags: ['building'],
  }),
  defineTerrain({
    id: 'barracks', name: '兵营', cost: costs(1, 1, 1, 1), defense: 0.2, capturable: true,
    ownerTurnGrants: [{ resource: FUNDS_RESOURCE, amount: 50 }], heal: 20, produces: ['soldier', 'archer', 'rogue', 'cleric'], tags: ['building', 'production'],
  }),
  defineTerrain({
    id: 'castle', name: '城堡', cost: costs(1, 1, 1, 1), defense: 0.3, capturable: true,
    ownerTurnGrants: [{ resource: FUNDS_RESOURCE, amount: 100 }], heal: 30, hq: true,
    produces: ['soldier', 'archer', 'rogue', 'cleric', 'mage', 'knight', 'ogre', 'ballista', 'dragon'],
    tags: ['building', 'production', 'hq'],
  }),
];

export const ANCIENT_EMPIRES_TERRAIN_CHARACTERS: Readonly<Record<string, TerrainId>> = {
  '.': 'plain',
  '-': 'road',
  '=': 'bridge',
  T: 'forest',
  h: 'hill',
  '^': 'mountain',
  '~': 'water',
  '#': 'wall',
  v: 'village',
  b: 'barracks',
  C: 'castle',
};
