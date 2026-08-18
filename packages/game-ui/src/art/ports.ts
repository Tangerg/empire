import type {
  StructureTypeId,
  TerrainId,
  UnitTypeId,
  WeaponId,
} from '@empire/battle-engine';
export interface RuntimeTerrainContext {
  x: number;
  y: number;
  theme?: string;
  ownerColor?: string;
  linked: { n: boolean; e: boolean; s: boolean; w: boolean };
}

export interface ArtProvider {
  id: string;
  unitMarkup?(type: UnitTypeId, team: string): string | null;
  unitIcon?(type: UnitTypeId, team: string, size: number): string | null;
  terrainMarkup?(id: TerrainId, context: RuntimeTerrainContext): string | null;
  portraitMarkup?(type: UnitTypeId, team: string): string | null;
  structureMarkup?(type: StructureTypeId, ownerColor?: string): string | null;
  iconMarkup?(topic: string, size?: number, className?: string): string | null;
  abilityIcon?(ability: string): string | null;
  weaponIcon?(weapon: WeaponId): string | null;
  statusIcon?(status: string): string | null;
  coverMarkup?(level: 'half' | 'full'): string | null;
  markerMarkup?(topic: string): string | null;
  weaponFx?(weapon: WeaponId): string | null;
  effectMarkup?(topic: string, cx?: number, cy?: number): string | null;
}


