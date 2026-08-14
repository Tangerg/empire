import { hostileCastsAgainst } from '../casting';
import { idx } from '../grid';
import { enemyUnitsOf } from '../state';
import type { ContentCatalog } from '../content-pack';
import type { GameState, PlayerId } from '../types';
import type { TacticalSpace } from '../tactical-space';
import { weaponCoverage, type WeaponAreaRules } from '../weapon-area';
import { hpRatio, maximumWeaponPower } from './measures';

/**
 * Total damage the enemy could land on each tile if everyone attacked it.
 *
 * Read once per turn and shared by every decision, because it costs a threat
 * projection per hostile unit and the answer cannot change until someone moves.
 */
/**
 * Port declared by this module: where a weapon reaches, and what a unit is worth.
 *
 * The caller used to pass the ruleset *and* pull `space` and `content` back out
 * of it as two more parameters — three services for one question, two of them
 * behind the subjects.
 */
export interface ThreatRules extends WeaponAreaRules {
  readonly space: TacticalSpace;
  readonly content: ContentCatalog;
}

export function threatMap(
  rules: ThreatRules,
  state: GameState,
  viewer: PlayerId,
): Map<number, number> {
  const { space, content } = rules;
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
    for (const cell of weaponCoverage(rules, state, cast.origin, cast.target, weapon.area)) {
      const tile = idx(state.map, cell.x, cell.y);
      threat.set(tile, (threat.get(tile) ?? 0) + weapon.power * 1.5);
    }
  }
  return threat;
}
