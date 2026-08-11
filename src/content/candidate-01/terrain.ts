import { defineTerrain } from '../../core/content-builders';
import type { MoveCosts, TerrainDef, TerrainId } from '../../core/types';

const costs = (
  foot: number | null,
  mounted: number | null,
  heavy: number | null,
  flying: number | null,
  naval: number | null = null,
  amphibious: number | null = foot,
): MoveCosts => ({ foot, mounted, heavy, flying, naval, amphibious });

/** Story-one battlefield materials.  They remain mechanics-first terrain data. */
export const CANDIDATE_01_TERRAINS: readonly TerrainDef[] = [
  defineTerrain({ id: 'c01.scorched', name: '焦土农田', cost: costs(2, 3, 3, 1), defense: 0.05, tags: ['rough', 'burned'] }),
  defineTerrain({ id: 'c01.riverbank', name: '泥泞河岸', cost: costs(2, 4, 4, 1), defense: 0.08, tags: ['rough', 'wet'] }),
  defineTerrain({ id: 'c01.street', name: '王都石街', cost: costs(1, 1, 1, 1), defense: 0.05, tags: ['road', 'urban'] }),
  defineTerrain({ id: 'c01.oathway', name: '受控誓文', cost: costs(2, 2, 2, 1), defense: -0.05, tags: ['oathbound', 'hazard'] }),
  defineTerrain({ id: 'c01.forge', name: '山炉石台', cost: costs(1, 2, 1, 1), defense: 0.18, cover: 'half', tags: ['forge', 'urban'] }),
  defineTerrain({ id: 'c01.graveyard', name: '无声墓园', cost: costs(2, 3, 3, 1), defense: 0.15, cover: 'half', tags: ['graveyard', 'rough'] }),
  defineTerrain({ id: 'c01.molten', name: '熔流沟渠', cost: costs(null, null, null, 1), tags: ['hazard', 'molten'] }),
  defineTerrain({ id: 'c01.mother-root', name: '银林母根', cost: costs(2, null, null, 1), defense: 0.25, cover: 'half', obstructionHeight: 2, opaque: true, tags: ['silverwood', 'rough'] }),
  defineTerrain({ id: 'c01.outpost', name: '战役据点', cost: costs(1, 1, 1, 1), defense: 0.2, capturable: true, heal: 15, tags: ['building', 'outpost'] }),
  defineTerrain({ id: 'c01.field-post', name: '临时军营', cost: costs(1, 1, 1, 1), defense: 0.15, capturable: true, heal: 15, tags: ['building', 'outpost'] }),
  defineTerrain({ id: 'c01.keep', name: '战役主堡', cost: costs(1, 1, 1, 1), defense: 0.3, capturable: true, heal: 25, hq: true, tags: ['building', 'hq'] }),
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
