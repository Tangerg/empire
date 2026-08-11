import type { FormationDef } from '../../core/types';

/** Theme-neutral tactical shapes. Content packs choose which units may use them. */
export const COMMON_FORMATIONS: readonly FormationDef[] = [
  {
    id: 'formation-line',
    name: '线列',
    attackMultiplier: 1.05,
    defenseDelta: 0.02,
    movementDelta: -1,
    minimumAdjacentAllies: 1,
    tags: ['ordered', 'offensive'],
  },
  {
    id: 'formation-defensive',
    name: '防御阵',
    attackMultiplier: 0.9,
    defenseDelta: 0.12,
    movementDelta: -1,
    minimumAdjacentAllies: 1,
    tags: ['ordered', 'defensive'],
  },
  {
    id: 'formation-loose',
    name: '散阵',
    attackMultiplier: 1,
    defenseDelta: 0,
    movementDelta: 0,
    minimumAdjacentAllies: 0,
    tags: ['mobile'],
  },
];

