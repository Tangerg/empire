import { Registry } from '../registry';
import type { TerrainDef } from '../types';

export const Terrains = new Registry<TerrainDef>('terrain');

export const terrainDef = (id: string): TerrainDef => Terrains.get(id);
