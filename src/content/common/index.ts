import type { ContentPack } from '../../core/content-pack';
import { COMMON_MOVEMENT_PROFILES } from './movement';
import { COMMON_TERRAIN_OVERLAYS } from './overlays';
import { COMMON_STATUSES } from './statuses';
import { COMMON_STRUCTURES } from './structures';
import { COMMON_TACTICS } from './tactics';
import { COMMON_FORMATIONS } from './formations';

export const COMMON_CONTENT_PACK: ContentPack = {
  id: 'empire.common',
  version: 1,
  movementProfiles: COMMON_MOVEMENT_PROFILES,
  statuses: COMMON_STATUSES,
  structures: COMMON_STRUCTURES,
  terrainOverlays: COMMON_TERRAIN_OVERLAYS,
  tactics: COMMON_TACTICS,
  formations: COMMON_FORMATIONS,
};

export * from './movement';
export * from './overlays';
export * from './statuses';
export * from './structures';
export * from './tactics';
export * from './formations';
