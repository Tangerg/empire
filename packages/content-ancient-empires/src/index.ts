import type { ContentPack } from '@empire/battle-engine';
import {
  ANCIENT_EMPIRES_ARMOR_CLASSES,
  ANCIENT_EMPIRES_DAMAGE_MATCHUPS,
  ANCIENT_EMPIRES_DAMAGE_TYPES,
} from './damage';
import {
  ANCIENT_EMPIRES_TERRAIN_CHARACTERS,
  ANCIENT_EMPIRES_TERRAINS,
} from './terrain';
import { ANCIENT_EMPIRES_UNITS } from './units';
import { ANCIENT_EMPIRES_WEAPONS } from './weapons';
import { ANCIENT_EMPIRES_CAREERS } from './careers';

export const ANCIENT_EMPIRES_CONTENT_PACK: ContentPack = {
  id: 'empire.ancient-empires',
  version: 1,
  dependencies: ['empire.common'],
  damageTypes: ANCIENT_EMPIRES_DAMAGE_TYPES,
  armorClasses: ANCIENT_EMPIRES_ARMOR_CLASSES,
  damageMatchups: ANCIENT_EMPIRES_DAMAGE_MATCHUPS,
  terrains: ANCIENT_EMPIRES_TERRAINS,
  terrainCharacters: ANCIENT_EMPIRES_TERRAIN_CHARACTERS,
  defaultTerrain: 'plain',
  weapons: ANCIENT_EMPIRES_WEAPONS,
  units: ANCIENT_EMPIRES_UNITS,
  careers: ANCIENT_EMPIRES_CAREERS,
};

export * from './terrain';
export * from './damage';
export * from './units';
export * from './weapons';
export * from './careers';
export * from './levels';
