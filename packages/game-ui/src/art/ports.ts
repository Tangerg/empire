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
/**
 * The terrain tags the art reads to knit one cell's ground to the next.
 *
 * Stated once, because two different questions ask it and both used to spell the
 * answer themselves: `links` below decides whether a neighbour's *picture* joins
 * this one, and a painted scene's route mask decides which way a road runs. The
 * shared part is the vocabulary — what the ruleset calls a way, a threshold and an
 * obstacle — and a vocabulary written in three places is three chances to forget
 * `outpost`.
 */
export const GROUND_TAGS = {
  /** Ground a traveller moves along. */
  way: ['road'],
  /** Somewhere people live, which a way runs *into* rather than stopping beside. */
  settlement: ['building', 'outpost'],
  /** Something standing in the way, which joins its own kind and nothing else. */
  obstacle: ['blocking'],
} as const;

/** Does this terrain carry any of those tags? */
export const taggedGround = (
  terrain: { readonly tags: readonly string[] },
  ...families: readonly (readonly string[])[]
): boolean => families.some((family) => family.some((tag) => terrain.tags.includes(tag)));

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


