import type {
  BattlefieldMarker,
  GameMap,
  StructureDef,
  StructureState,
  WeaponId,
} from '@empire/battle-engine';
import { SquareBoardDecorations, type BoardDecorations } from './board-decorations';
import { markerFromRules, structureFromRules } from './field-objects-from-rules';
import type {
  SceneFrameMarkup,
  SceneLayerMarkup,
  SceneViewport,
  SceneViewportProfile,
} from './scene-viewport';

export interface BattlePresentation {
  id: string;
  boardClass?: string;
  /**
   * How the tactical layer is drawn over this art. The board used to derive it
   * from `id === 'generic'`, so only two looks existed and only these two ids
   * could have them.
   */
  decorations?: BoardDecorations;
  matches(levelId: string): boolean;
  sceneProfile(levelId: string): SceneViewportProfile;
  sceneFrame(levelId: string, map: GameMap, viewport: SceneViewport): SceneFrameMarkup;
  /**
   * Art drawn inside the tactical rectangle.
   *
   * The viewport comes along because a layer that spans the field has to know
   * how wide the field is, and `map.width * TILE` is the square tiling written
   * into the port — a hex board of the same columns is half a cell wider.
   */
  sceneLayers(levelId: string, map: GameMap, viewport: SceneViewport): SceneLayerMarkup;
  /**
   * How this art draws a destructible structure, or `null` for no opinion.
   *
   * `null` means "ask the floor", not "draw nothing" — the same convention every
   * `ArtProvider` method already uses. A thing the rules track may not be
   * invisible: `c01-15` asks the player to destroy a 500 HP structure whose type
   * the campaign's art has no topic for, and it was drawn as nothing at all.
   */
  structure(state: StructureState, def: StructureDef, ownerColor?: string): string | null;
  /** A mark left on the ground, or `null` for no opinion. */
  marker(marker: BattlefieldMarker, ownerColor?: string): string | null;
  weaponFx(weapon: WeaponId): string | null;
  /**
   * A named effect, drawn about its own origin.
   *
   * It used to take the scene coordinates to draw at, so an effect's position was
   * baked into its picture — the one thing left on the board that a renderer could
   * not treat as a drawing at a place. Whoever plays it places it.
   */
  effect(topic: string): string;
  healFx?: string;
}

const EMPTY_FRAME: SceneFrameMarkup = Object.freeze({ backdrop: '', foreground: '' });

/**
 * Light on an unpainted field.
 *
 * A level nobody painted a scene for was a rectangle of tiles that stopped dead
 * at its own edge — every tile lit exactly like every other, and the border a cut
 * rather than a horizon. This is the smallest thing that makes it a place: a
 * warm fall of light from above and the ground darkening as it runs away from
 * you. It costs no tactical area, which the alternative — an authored margin
 * around the field — would have, at about a tenth of the board.
 *
 * The gradient ids are fixed rather than serialised: the definitions are
 * identical wherever they appear, so two boards in one document sharing them is
 * the desired outcome rather than a collision.
 */
function genericFieldLight(viewport: SceneViewport): string {
  const width = viewport.fieldWidth;
  const height = viewport.fieldHeight;
  return `<defs>
      <radialGradient id="field-sun" cx="0.5" cy="0.02" r="0.92">
        <stop offset="0" stop-color="#fff4d8" stop-opacity="0.17"/>
        <stop offset="0.5" stop-color="#fff4d8" stop-opacity="0.05"/>
        <stop offset="1" stop-color="#fff4d8" stop-opacity="0"/>
      </radialGradient>
      <radialGradient id="field-rim" cx="0.5" cy="0.48" r="0.66">
        <stop offset="0.5" stop-color="#0b0806" stop-opacity="0"/>
        <stop offset="0.84" stop-color="#0b0806" stop-opacity="0.2"/>
        <stop offset="1" stop-color="#0b0806" stop-opacity="0.5"/>
      </radialGradient>
    </defs>
    <g class="field-light" pointer-events="none">
      <rect width="${width}" height="${height}" fill="url(#field-sun)"/>
      <rect width="${width}" height="${height}" fill="url(#field-rim)"/>
    </g>`;
}

/** The look a level gets when no painted scene claims it. */
export const GENERIC_PRESENTATION: BattlePresentation = Object.freeze({
  id: 'generic',
  decorations: SquareBoardDecorations,
  matches: () => true,
  sceneProfile: () => ({}),
  sceneFrame: () => EMPTY_FRAME,
  sceneLayers: (_levelId: string, _map: GameMap, viewport: SceneViewport) => ({
    ground: '',
    underUnits: genericFieldLight(viewport),
    overUnits: '',
  }),
  // The floor under every presentation, and therefore total: whatever a painted
  // scene declines is still drawn, from what the rules can see about it.
  structure: structureFromRules,
  marker: markerFromRules,
  weaponFx: () => null,
  effect: () => '',
});

/**
 * How the board draws its tactical layer for this art. Painted scenes without a
 * stated preference keep the grid, which is the look that works over anything.
 */
export const decorationsFor = (presentation: BattlePresentation): BoardDecorations =>
  presentation.decorations ?? SquareBoardDecorations;

