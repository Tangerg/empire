import type { ContentPack } from '@empire/battle-engine';
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

// The package entry is the composed product. The two movement helpers are the
// only authoring primitives another content pack consumes; definition arrays
// remain implementation details available through `COMMON_CONTENT_PACK`.
export { IMPASSABLE, moveCosts } from './movement';
