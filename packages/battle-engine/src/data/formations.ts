import { Registry } from '../registry';
import type { FormationDef } from '../types';

/** Global compatibility registry; isolated engines receive a cloned catalog. */
export const Formations = new Registry<FormationDef>('formation');

