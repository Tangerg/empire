import type { ContentPack } from '@empire/battle-engine';
import { CANDIDATE_01_STATUSES, CANDIDATE_01_TACTICS } from './tactics';
import { CANDIDATE_01_UNITS } from './units';
import { CANDIDATE_01_WEAPONS } from './weapons';
import { CANDIDATE_01_TERRAINS, CANDIDATE_01_TERRAIN_CHARACTERS } from './terrain';
import { CANDIDATE_01_STRUCTURES } from './structures';

export const CANDIDATE_01_CONTENT_PACK: ContentPack = {
  id: 'candidate-01',
  version: 1,
  dependencies: ['empire.common', 'empire.ancient-empires'],
  terrains: CANDIDATE_01_TERRAINS,
  terrainCharacters: CANDIDATE_01_TERRAIN_CHARACTERS,
  weapons: CANDIDATE_01_WEAPONS,
  units: CANDIDATE_01_UNITS,
  statuses: CANDIDATE_01_STATUSES,
  tactics: CANDIDATE_01_TACTICS,
  structures: CANDIDATE_01_STRUCTURES,
};

export { CANDIDATE_01_LEVELS, candidate01Level } from './levels';
export { CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN } from './campaign';
