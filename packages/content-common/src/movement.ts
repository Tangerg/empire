import type { MoveCosts, MovementProfileDef } from '@empire/battle-engine';

/** Cross-theme movement semantics. Packs may add hover, phase, rail, etc. */
export const COMMON_MOVEMENT_PROFILES: readonly MovementProfileDef[] = [
  { id: 'foot', name: '步行', tags: ['ground'], maxClimb: 1, maxDrop: 2, uphillCostPerLevel: 1, ignoresCliffs: false },
  { id: 'mounted', name: '骑乘', tags: ['ground', 'fast'], maxClimb: 1, maxDrop: 1, uphillCostPerLevel: 2, ignoresCliffs: false },
  { id: 'heavy', name: '重装', tags: ['ground', 'heavy'], maxClimb: 1, maxDrop: 1, uphillCostPerLevel: 2, ignoresCliffs: false },
  { id: 'flying', name: '飞行', tags: ['air'], maxClimb: null, maxDrop: null, uphillCostPerLevel: 0, ignoresCliffs: true },
  { id: 'naval', name: '水军', tags: ['water'], maxClimb: 0, maxDrop: 0, uphillCostPerLevel: 0, ignoresCliffs: false },
  { id: 'amphibious', name: '两栖', tags: ['ground', 'water'], maxClimb: 1, maxDrop: 2, uphillCostPerLevel: 1, ignoresCliffs: false },
];

/**
 * Movement costs for one terrain, in this pack's six classes.
 *
 * Here rather than in the engine, because these names are content: the engine's
 * `MovementClass` is a string backed by whatever profiles a catalog installed,
 * and it must not learn that `mounted` exists. Here rather than in each pack,
 * because it was copied verbatim into two of them — and a third story would have
 * copied it again.
 *
 * Named rather than positional. The call sites read `costs(2, 3, 3, 1)`: six
 * numbers whose only meaning was their order, in a list whose order this file
 * owns. `naval` and `amphibious` keep their old defaults — most ground is not
 * water, and something that crosses water on foot also crosses it as foot does.
 *
 * A class left out is impassable, which is what `Battlefield.movementCost`
 * already answers for a cost it cannot find, so a pack that adds `hover` gets a
 * safe default rather than free passage.
 */
export function moveCosts(costs: {
  foot: number | null;
  mounted: number | null;
  heavy: number | null;
  flying: number | null;
  naval?: number | null;
  amphibious?: number | null;
}): MoveCosts {
  return {
    foot: costs.foot,
    mounted: costs.mounted,
    heavy: costs.heavy,
    flying: costs.flying,
    naval: costs.naval ?? null,
    amphibious: costs.amphibious ?? costs.foot,
  };
}

/** Ground nothing in this pack's classes can enter. */
export const IMPASSABLE: MoveCosts = moveCosts({ foot: null, mounted: null, heavy: null, flying: null });
