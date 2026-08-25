import type { TerrainId, UnitTypeId, WeaponId } from '@empire/battle-engine';
import type { BoardPicture } from './board-surface';

/**
 * Everything a painter may know about one cell it is asked to draw.
 *
 * Declared once, here, because it is the port's own vocabulary. It used to be
 * three identical declarations — this one as `RuntimeTerrainContext`, a private
 * `TileContext` in `terrain.ts`, and a private copy of the first in the campaign
 * — so a field added to the port would have reached exactly one of the three
 * painters that read it, and the two names for one thing hid that.
 */
export interface TileContext {
  x: number;
  y: number;
  /** Presentation theme; ignored by mechanics and generic painters. */
  theme?: string | undefined;
  /** Owner colour for buildings; undefined = neutral. */
  ownerColor?: string | undefined;
  /** Same-terrain neighbours, used to knit roads and water together. */
  linked: { n: boolean; e: boolean; s: boolean; w: boolean };
}

export interface ArtProvider {
  id: string;
  unitPicture?(type: UnitTypeId, team: string): BoardPicture | null;
  unitIcon?(type: UnitTypeId, team: string, size: number): string | null;
  terrainMarkup?(id: TerrainId, context: TileContext): string | null;
  portraitMarkup?(type: UnitTypeId, team: string): string | null;
  iconMarkup?(topic: string, size?: number, className?: string): string | null;
  abilityIcon?(ability: string): string | null;
  weaponIcon?(weapon: WeaponId): string | null;
  statusIcon?(status: string): string | null;
  coverMarkup?(level: 'half' | 'full'): string | null;
  /*
   * There were four more: `effectMarkup?(topic, cx?, cy?)`, `structureMarkup`,
   * `markerMarkup` and `weaponFx`.
   *
   * The campaign assigned every one of them and nothing in any package ever
   * called them — port fields their own module never read. Three were shadows of
   * live `BattlePresentation` fields: a structure, a mark on the ground and a
   * weapon's effect are all things a *scene* draws, so the presentation is where
   * they are asked for, and two of the three were assigned `() => null` here — a
   * provider saying nothing through a field nobody was listening to.
   *
   * `effectMarkup`'s `cx`/`cy` were the older mistake still visible: an effect
   * used to be drawn at scene coordinates rather than about its own origin.
   */
}


