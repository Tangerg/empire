import type {
  BattlefieldMarker,
  GameMap,
  StructureDef,
  StructureState,
  WeaponId,
} from '@empire/battle-engine/types';
import { SquareBoardDecorations, type BoardDecorations } from './board-decorations';
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
  sceneLayers(levelId: string, map: GameMap): SceneLayerMarkup;
  structure(state: StructureState, def: StructureDef, ownerColor?: string): string | null;
  marker(marker: BattlefieldMarker): string;
  weaponFx(weapon: WeaponId): string | null;
  effect(topic: string, cx: number, cy: number): string;
  healFx?: string;
}

const EMPTY_FRAME: SceneFrameMarkup = Object.freeze({ backdrop: '', foreground: '' });
const EMPTY_SCENE_LAYERS: SceneLayerMarkup = Object.freeze({
  ground: '',
  underUnits: '',
  overUnits: '',
});

const GENERIC_PRESENTATION: BattlePresentation = Object.freeze({
  id: 'generic',
  decorations: SquareBoardDecorations,
  matches: () => true,
  sceneProfile: () => ({}),
  sceneFrame: () => EMPTY_FRAME,
  sceneLayers: () => EMPTY_SCENE_LAYERS,
  structure: () => null,
  marker: () => '',
  weaponFx: () => null,
  effect: () => '',
});

const presentations: BattlePresentation[] = [];

export function registerBattlePresentation(presentation: BattlePresentation): () => void {
  if (presentations.some((entry) => entry.id === presentation.id)) return () => {};
  presentations.unshift(presentation);
  return () => {
    const index = presentations.indexOf(presentation);
    if (index >= 0) presentations.splice(index, 1);
  };
}

/**
 * How the board draws its tactical layer for this art. Painted scenes without a
 * stated preference keep the grid, which is the look that works over anything.
 */
export const decorationsFor = (presentation: BattlePresentation): BoardDecorations =>
  presentation.decorations ?? SquareBoardDecorations;

/** Resolve art policy at the composition edge; the board stays story-agnostic. */
export function battlePresentation(levelId: string): BattlePresentation {
  return presentations.find((presentation) => presentation.matches(levelId)) ?? GENERIC_PRESENTATION;
}
