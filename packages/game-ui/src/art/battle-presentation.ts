import type {
  BattlefieldMarker,
  GameMap,
  StructureDef,
  StructureState,
  WeaponId,
} from '@empire/battle-engine/types';
import type {
  SceneFrameMarkup,
  SceneLayerMarkup,
  SceneViewport,
  SceneViewportProfile,
} from './scene-viewport';

export interface BattlePresentation {
  id: string;
  boardClass?: string;
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

/** Resolve art policy at the composition edge; the board stays story-agnostic. */
export function battlePresentation(levelId: string): BattlePresentation {
  return presentations.find((presentation) => presentation.matches(levelId)) ?? GENERIC_PRESENTATION;
}
