import { defineTerrainOverlay } from '../../core/content-builders';
import type { TerrainOverlayDef } from '../../core/types';

export const COMMON_TERRAIN_OVERLAYS: readonly TerrainOverlayDef[] = [
  defineTerrainOverlay({
    id: 'flooded',
    name: '洪水',
    movementCostDelta: 1,
    blockedMovementClasses: ['mounted'],
    defenseDelta: -0.1,
    tags: ['water', 'weather'],
  }),
  defineTerrainOverlay({
    id: 'fire_field',
    name: '火场',
    movementCostDelta: 1,
    defenseDelta: -0.1,
    turnStartStatus: { id: 'poisoned', duration: 2 },
    tags: ['fire', 'hazard'],
  }),
  defineTerrainOverlay({
    id: 'vacuum',
    name: '真空区',
    turnStartStatus: { id: 'shaken', duration: 2 },
    tags: ['vacuum', 'hazard'],
  }),
  defineTerrainOverlay({
    id: 'low_gravity',
    name: '低重力区',
    movementCostDelta: -1,
    visionDelta: 1,
    tags: ['gravity'],
  }),
  defineTerrainOverlay({
    id: 'signal_storm',
    name: '干扰风暴',
    movementCostDelta: 1,
    visionDelta: -2,
    turnStartStatus: { id: 'shaken', duration: 2 },
    tags: ['weather', 'interference'],
  }),
];
