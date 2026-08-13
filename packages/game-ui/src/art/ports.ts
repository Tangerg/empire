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

export interface RuntimeArtAsset {
  url: string;
  width: number;
  height: number;
  frameWidth?: number;
  frameHeight?: number;
  frames?: number;
  anchor?: [number, number];
  footprint?: [number, number];
  fps?: number;
  loop?: boolean;
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

const providers: ArtProvider[] = [];

export function registerArtProvider(provider: ArtProvider): () => void {
  if (providers.some((entry) => entry.id === provider.id)) return () => {};
  providers.unshift(provider);
  return () => {
    const index = providers.indexOf(provider);
    if (index >= 0) providers.splice(index, 1);
  };
}

export function resolveArt<T>(select: (provider: ArtProvider) => T | null | undefined): T | null {
  for (const provider of providers) {
    const result = select(provider);
    if (result != null) return result;
  }
  return null;
}

export function resetArtProviders(): void {
  providers.splice(0);
}
