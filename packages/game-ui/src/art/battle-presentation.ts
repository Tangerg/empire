import type {
  BattlefieldMarker,
  ContentCatalog,
  GameMap,
  StructureDef,
  StructureState,
  WeaponId,
} from '@empire/battle-engine';
import { SquareBoardDecorations, type BoardDecorations } from './board-decorations';
import { wholeField, type BoardPicture } from './board-surface';
import { markerFromRules, structureFromRules } from './field-objects-from-rules';
import type {
  SceneFrameMarkup,
  SceneLayers,
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
  /*
   * There was a `matches(levelId)` here, and an `ArtDirection` held a *list* of
   * presentations that it searched with it.
   *
   * It read like per-level selection between painted packs. What it actually did
   * was let a pack decline a level the application root had already handed it:
   * this campaign's predicate was `levelId.startsWith('c01-')`, and its painted
   * composition was gated behind a second allowlist inside that, so one chapter
   * of sixteen was painted and every built-in level fell through to flat tiles.
   *
   * A scene is not a chain. One thing draws the ground, the root says which, and a
   * presentation that varies by level does it where the level id already arrives —
   * in `sceneProfile`, `sceneFrame` and `sceneLayers` below.
   */
  /**
   * Whether this art has sheets for cells of this shape, asked before it is
   * handed a board.
   *
   * A painted scene is cut to a tiling: this campaign's atlases are 32-unit
   * squares indexed by square neighbours, and there is no picture in them for a
   * hexagon. Handed one anyway it drew the whole ground at square coordinates —
   * cells in the wrong places, silently.
   *
   * A question rather than a refusal, because the two callers owe the player
   * different things. A battle drawn wrong is worse than a battle refused, so the
   * board throws. A *picture* of a map — a level card, the editor's canvas, a
   * palette swatch — still owes the reader the map, so it leaves the scene out and
   * shows the tiles. Absent means this art paints anything.
   */
  paintsCells?(corners: number): boolean;
  sceneProfile(levelId: string): SceneViewportProfile;
  sceneFrame(scene: BattleSceneContext): SceneFrameMarkup;
  /**
   * Art drawn inside the tactical rectangle.
   *
   * The viewport comes along because a layer that spans the field has to know
   * how wide the field is, and `map.width * TILE` is the square tiling written
   * into the port — a hex board of the same columns is half a cell wider.
   */
  sceneLayers(scene: BattleSceneContext): SceneLayers;
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
   *
   * A picture rather than markup, because an explosion is a strip that starts
   * playing when it appears, and that is the declaration a renderer acts on.
   */
  effect(topic: string): BoardPicture;
  healFx?: string;
}

/** Everything a presentation may read about one scene, explicitly injected. */
export interface BattleSceneContext {
  readonly content: ContentCatalog;
  readonly levelId: string;
  readonly map: GameMap;
  readonly viewport: SceneViewport;
}

const EMPTY_FRAME: SceneFrameMarkup = Object.freeze({ backdrop: '', foreground: '' });
/** No art of its own: the board still draws its own burst and its own number. */
const NO_PICTURE: BoardPicture = Object.freeze({ body: '' });

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

/** The look a shell gets when its art composes no painted scene. */
export const GENERIC_PRESENTATION: BattlePresentation = Object.freeze({
  id: 'generic',
  decorations: SquareBoardDecorations,
  sceneProfile: () => ({}),
  sceneFrame: () => EMPTY_FRAME,
  sceneLayers: ({ viewport }: BattleSceneContext) => ({
    ground: [],
    // A fall of light across the whole field: art with no place of its own.
    underUnits: wholeField(genericFieldLight(viewport)),
    overUnits: [],
  }),
  // The floor under every presentation, and therefore total: whatever a painted
  // scene declines is still drawn, from what the rules can see about it.
  structure: structureFromRules,
  marker: markerFromRules,
  weaponFx: () => null,
  effect: () => NO_PICTURE,
});

/**
 * Whether this art has sheets for a board whose cells have this many corners.
 *
 * Stated here rather than at each caller, so the board's refusal and a level
 * card's fallback are two readings of one answer.
 */
export const paintsCells = (presentation: BattlePresentation, corners: number): boolean =>
  presentation.paintsCells?.(corners) ?? true;

/**
 * How the board draws its tactical layer for this art. Painted scenes without a
 * stated preference keep the grid, which is the look that works over anything.
 */
export const decorationsFor = (presentation: BattlePresentation): BoardDecorations =>
  presentation.decorations ?? SquareBoardDecorations;
