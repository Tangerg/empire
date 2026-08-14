import { hostileCastsAgainst } from '../casting';
import { idx } from '../grid';
import { enemyUnitsOf } from '../state';
import type { ContentCatalog } from '../content-pack';
import type { GameState, PlayerId } from '../types';
import type { TacticalSpace } from '../tactical-space';
import type { WeaponAreaRules } from '../weapon-area';
import { hpRatio, maximumWeaponPower } from './measures';

/**
 * Total damage the enemy could land on each tile if everyone attacked it.
 *
 * Read once per turn and shared by every decision, because it costs a threat
 * projection per hostile unit and the answer cannot change until someone moves.
 */
export function threatMap(
  rules: WeaponAreaRules,
  state: GameState,
  viewer: PlayerId,
  space: TacticalSpace,
  content: ContentCatalog,
): Map<number, number> {
  const threat = new Map<number, number>();
  for (const foe of enemyUnitsOf(state, viewer)) {
    const weight = maximumWeaponPower(foe.type, content) * (0.5 + 0.5 * hpRatio(foe, content));
    for (const tile of space.threatOf(state, foe)) {
      threat.set(tile, (threat.get(tile) ?? 0) + weight);
    }
  }
  // A tile already marked by a charging strike is known danger, not potential
  // danger, so it weighs more than a tile someone merely could reach.
  for (const cast of hostileCastsAgainst(state, viewer)) {
    const weapon = content.weapons.get(cast.weapon);
    for (const cell of rules.areaShapes.coverage(state.map, cast.origin, cast.target, weapon.area)) {
      const tile = idx(state.map, cell.x, cell.y);
      threat.set(tile, (threat.get(tile) ?? 0) + weapon.power * 1.5);
    }
  }
  return threat;
}
