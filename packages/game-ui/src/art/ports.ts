import type {
  StructureTypeId,
  TerrainId,
  UnitTypeId,
  WeaponId,
} from '@empire/battle-engine';
import type { BoardPicture } from './board-surface';

export interface RuntimeTerrainContext {
  x: number;
  y: number;
  theme?: string;
  ownerColor?: string;
  linked: { n: boolean; e: boolean; s: boolean; w: boolean };
}

export interface ArtProvider {
  id: string;
  unitPicture?(type: UnitTypeId, team: string): BoardPicture | null;
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
  /*
   * There was an `effectMarkup?(topic, cx?, cy?)` here.
   *
   * The campaign assigned it, and nothing in any package ever called it — a port
   * field its own module never read. An effect's picture reaches the board through
   * `BattlePresentation.effect`, which is where a look that belongs to a painted
   * scene belongs. The `cx`/`cy` parameters were the older mistake still visible:
   * an effect used to be drawn at scene coordinates rather than about its origin.
   */
}


