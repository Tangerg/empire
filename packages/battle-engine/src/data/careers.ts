import { Registry } from '../registry';
import type { CareerDef, CareerId, UnitTypeId } from '../types';

export const Careers = new Registry<CareerDef>('career');

export const careerDef = (id: CareerId): CareerDef => Careers.get(id);

export function careersForUnitType(unitType: UnitTypeId): CareerDef[] {
  return Careers.all().filter((career) => career.unitType === unitType);
}

export function defaultCareerForUnitType(unitType: UnitTypeId): CareerDef | undefined {
  return careersForUnitType(unitType)
    .sort((left, right) => left.tier - right.tier || left.id.localeCompare(right.id))[0];
}
