import { unitWeapons } from '../combat';
import { gaugeRatio } from '../vitals';
import type { ContentCatalog } from '../content-pack';
import type { Unit, UnitTypeId, WeaponDef } from '../types';

/**
 * The scalar vocabulary the planner reasons in.
 *
 * Every one of these is a question about a unit or a tile that more than one
 * part of planning asks — how hard it hits, how far it reaches, how much it is
 * worth losing. Kept together so the answers cannot differ between the tile
 * appraisal, the ability evaluators and the recruiter.
 */

export const weaponsForType = (type: UnitTypeId, content: ContentCatalog): WeaponDef[] =>
  content.units.get(type).weapons.map((id) => content.weapons.get(id));

export const maximumWeaponPower = (type: UnitTypeId, content: ContentCatalog): number =>
  Math.max(0, ...weaponsForType(type, content).map((weapon) => weapon.power));

export const maximumWeaponRange = (type: UnitTypeId, content: ContentCatalog): number =>
  Math.max(0, ...weaponsForType(type, content).map((weapon) => weapon.maxRange));

/** Melee wants contact; a pure shooter wants the far edge of its reach. */
export const preferredEngagementRange = (unit: Unit, content: ContentCatalog): number => {
  const weapons = unitWeapons(content, unit);
  if (weapons.some((weapon) => weapon.minRange <= 1)) return 1;
  return Math.max(1, ...weapons.map((weapon) => weapon.maxRange));
};

export const hpRatio = (unit: Unit, content: ContentCatalog): number =>
  gaugeRatio(unit.hp, content.units.get(unit.type).maxHp);

/**
 * What losing this unit would cost, as opposed to `UnitDef.value`, which is
 * what the *type* is worth. A wounded veteran and a fresh recruit of the same
 * type are not the same loss, and conflating the two is how a planner talks
 * itself into trading its last hero for a scout.
 */
export const unitWorth = (unit: Unit, content: ContentCatalog): number =>
  content.units.get(unit.type).value * (0.4 + 0.6 * hpRatio(unit, content)) * (1 + unit.rank * 0.08);
