import { defineTerrain, FUNDS_RESOURCE, type TerrainDef, type TerrainId } from '@empire/battle-engine';
import { IMPASSABLE, moveCosts } from '@empire/content-common';

export const ANCIENT_EMPIRES_TERRAINS: readonly TerrainDef[] = [
  defineTerrain({ id: 'plain', name: '平原', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }) }),
  defineTerrain({ id: 'road', name: '道路', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), tags: ['road'] }),
  defineTerrain({ id: 'bridge', name: '桥梁', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), tags: ['road'] }),
  defineTerrain({
    id: 'forest', name: '森林', cost: moveCosts({ foot: 2, mounted: 3, heavy: 3, flying: 1 }), defense: 0.2, cover: 'half', obstructionHeight: 2, opaque: true, tags: ['rough'],
  }),
  defineTerrain({
    id: 'hill', name: '丘陵', cost: moveCosts({ foot: 2, mounted: 3, heavy: 3, flying: 1 }), defense: 0.3, vision: 1, tags: ['rough', 'high'],
  }),
  defineTerrain({
    id: 'mountain', name: '山地', cost: moveCosts({ foot: 3, mounted: null, heavy: null, flying: 1 }), defense: 0.4, cover: 'half', obstructionHeight: 2, vision: 2, opaque: true, tags: ['rough', 'high'],
  }),
  defineTerrain({ id: 'water', name: '水域', cost: moveCosts({ foot: null, mounted: null, heavy: null, flying: 1, naval: 1, amphibious: 1 }), tags: ['water'] }),
  defineTerrain({ id: 'wall', name: '城墙', cost: IMPASSABLE, cover: 'full', obstructionHeight: 3, opaque: true, tags: ['blocking'] }),
  defineTerrain({
    id: 'village', name: '村庄', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), defense: 0.15, capturable: true,
    ownerTurnGrants: [{ resource: FUNDS_RESOURCE, amount: 100 }], heal: 20, tags: ['building'],
  }),
  defineTerrain({
    id: 'barracks', name: '兵营', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), defense: 0.2, capturable: true,
    ownerTurnGrants: [{ resource: FUNDS_RESOURCE, amount: 50 }], heal: 20, produces: ['soldier', 'archer', 'rogue', 'cleric'], tags: ['building', 'production'],
  }),
  defineTerrain({
    id: 'castle', name: '城堡', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), defense: 0.3, capturable: true,
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
