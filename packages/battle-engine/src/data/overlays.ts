import { Registry } from '../registry';
import type { OverlayTypeId, TerrainOverlayDef } from '../types';

export const TerrainOverlays = new Registry<TerrainOverlayDef>('terrain overlay');

export const terrainOverlayDef = (id: OverlayTypeId): TerrainOverlayDef => TerrainOverlays.get(id);
