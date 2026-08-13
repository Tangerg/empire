import { Registry } from '../registry';
import type { StructureDef, StructureTypeId } from '../types';

export const Structures = new Registry<StructureDef>('structure');

export const structureDef = (id: StructureTypeId): StructureDef => Structures.get(id);
