import type { CareerDef, UnitTypeId } from '../types';
import type { ContentRegistry } from '../registry';

export function careersForUnitType(
  careers: ContentRegistry<CareerDef>,
  unitType: UnitTypeId,
): CareerDef[] {
  return careers.all().filter((career) => career.unitType === unitType);
}

export function defaultCareerForUnitType(
  careers: ContentRegistry<CareerDef>,
  unitType: UnitTypeId,
): CareerDef | undefined {
  return careersForUnitType(careers, unitType)
    .sort((left, right) => left.tier - right.tier || left.id.localeCompare(right.id))[0];
}
