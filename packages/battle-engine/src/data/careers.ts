import type { CareerDef, UnitTypeId } from '../types';
import type { Registry } from '../registry';

export function careersForUnitType(
  careers: Registry<CareerDef>,
  unitType: UnitTypeId,
): CareerDef[] {
  return careers.all().filter((career) => career.unitType === unitType);
}

export function defaultCareerForUnitType(
  careers: Registry<CareerDef>,
  unitType: UnitTypeId,
): CareerDef | undefined {
  return careersForUnitType(careers, unitType)
    .sort((left, right) => left.tier - right.tier || left.id.localeCompare(right.id))[0];
}
