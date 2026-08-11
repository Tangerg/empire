import type { MovementProfileDef } from '../../core/types';

/** Cross-theme movement semantics. Packs may add hover, phase, rail, etc. */
export const COMMON_MOVEMENT_PROFILES: readonly MovementProfileDef[] = [
  { id: 'foot', name: '步行', tags: ['ground'], maxClimb: 1, maxDrop: 2, uphillCostPerLevel: 1, ignoresCliffs: false },
  { id: 'mounted', name: '骑乘', tags: ['ground', 'fast'], maxClimb: 1, maxDrop: 1, uphillCostPerLevel: 2, ignoresCliffs: false },
  { id: 'heavy', name: '重装', tags: ['ground', 'heavy'], maxClimb: 1, maxDrop: 1, uphillCostPerLevel: 2, ignoresCliffs: false },
  { id: 'flying', name: '飞行', tags: ['air'], maxClimb: null, maxDrop: null, uphillCostPerLevel: 0, ignoresCliffs: true },
  { id: 'naval', name: '水军', tags: ['water'], maxClimb: 0, maxDrop: 0, uphillCostPerLevel: 0, ignoresCliffs: false },
  { id: 'amphibious', name: '两栖', tags: ['ground', 'water'], maxClimb: 1, maxDrop: 2, uphillCostPerLevel: 1, ignoresCliffs: false },
];
