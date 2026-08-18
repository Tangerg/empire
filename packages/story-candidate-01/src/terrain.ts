import { defineTerrain, type TerrainDef, type TerrainId } from '@empire/battle-engine';
import { moveCosts } from '@empire/content-common';

/** Story-one battlefield materials.  They remain mechanics-first terrain data. */
export const CANDIDATE_01_TERRAINS: readonly TerrainDef[] = [
  defineTerrain({ id: 'c01.scorched', name: '焦土农田', cost: moveCosts({ foot: 2, mounted: 3, heavy: 3, flying: 1 }), defense: 0.05, tags: ['rough', 'burned'] }),
  defineTerrain({ id: 'c01.riverbank', name: '泥泞河岸', cost: moveCosts({ foot: 2, mounted: 4, heavy: 4, flying: 1 }), defense: 0.08, tags: ['rough', 'wet'] }),
  defineTerrain({ id: 'c01.street', name: '王都石街', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), defense: 0.05, tags: ['road', 'urban'] }),
  defineTerrain({ id: 'c01.oathway', name: '受控誓文', cost: moveCosts({ foot: 2, mounted: 2, heavy: 2, flying: 1 }), defense: -0.05, tags: ['oathbound', 'hazard'] }),
  defineTerrain({ id: 'c01.forge', name: '山炉石台', cost: moveCosts({ foot: 1, mounted: 2, heavy: 1, flying: 1 }), defense: 0.18, cover: 'half', tags: ['forge', 'urban'] }),
  defineTerrain({ id: 'c01.graveyard', name: '无声墓园', cost: moveCosts({ foot: 2, mounted: 3, heavy: 3, flying: 1 }), defense: 0.15, cover: 'half', tags: ['graveyard', 'rough'] }),
  defineTerrain({ id: 'c01.molten', name: '熔流沟渠', cost: moveCosts({ foot: null, mounted: null, heavy: null, flying: 1 }), tags: ['hazard', 'molten'] }),
  defineTerrain({ id: 'c01.mother-root', name: '银林母根', cost: moveCosts({ foot: 2, mounted: null, heavy: null, flying: 1 }), defense: 0.25, cover: 'half', obstructionHeight: 2, opaque: true, tags: ['silverwood', 'rough'] }),
  defineTerrain({ id: 'c01.outpost', name: '战役据点', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), defense: 0.2, capturable: true, heal: 15, tags: ['building', 'outpost'] }),
  defineTerrain({ id: 'c01.field-post', name: '临时军营', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), defense: 0.15, capturable: true, heal: 15, tags: ['building', 'outpost'] }),
  defineTerrain({ id: 'c01.keep', name: '战役主堡', cost: moveCosts({ foot: 1, mounted: 1, heavy: 1, flying: 1 }), defense: 0.3, capturable: true, heal: 25, hq: true, tags: ['building', 'hq'] }),
];

export const CANDIDATE_01_TERRAIN_CHARACTERS: Readonly<Record<string, TerrainId>> = {
  s: 'c01.scorched',
  r: 'c01.riverbank',
  p: 'c01.street',
  o: 'c01.oathway',
  f: 'c01.forge',
  g: 'c01.graveyard',
  m: 'c01.molten',
  R: 'c01.mother-root',
  q: 'c01.outpost',
  B: 'c01.field-post',
  K: 'c01.keep',
};
