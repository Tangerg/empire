import { Registry } from '../registry';
import { movementProfile } from './movement';
import type { UnitDef } from '../types';

export const UnitTypes = new Registry<UnitDef>('unit');

export const unitDef = (id: string): UnitDef => UnitTypes.get(id);

/** Presentation adapters resolve this lazily because content packs install at composition time. */
export const movementLabel = (id: string): string => movementProfile(id).name;
