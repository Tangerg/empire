import type {
  BattlefieldMarker,
  GameMap,
  StructureDef,
  StructureState,
  WeaponId,
} from '../core/types';
import {
  candidate01MapSceneryMarkup,
  candidate01SceneFrameMarkup,
  candidate01SceneProfile,
} from './candidate-01-map-scene';
import {
  candidate01FxMarkup,
  candidate01MarkerMarkup,
  candidate01StructureMarkup,
  candidate01WeaponFxTopic,
} from './candidate-01-runtime';
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

const CANDIDATE_01_PRESENTATION: BattlePresentation = Object.freeze({
  id: 'candidate-01',
  boardClass: 'candidate-map',
  matches: (levelId: string) => levelId.startsWith('c01-'),
  sceneProfile: candidate01SceneProfile,
  sceneFrame: candidate01SceneFrameMarkup,
  sceneLayers: candidate01MapSceneryMarkup,
  structure: candidate01StructureMarkup,
  marker: candidate01MarkerMarkup,
  weaponFx: candidate01WeaponFxTopic,
  effect: candidate01FxMarkup,
  healFx: 'C01-FX-17',
});

const PRESENTATIONS: readonly BattlePresentation[] = [CANDIDATE_01_PRESENTATION];

/** Resolve art policy at the composition edge; the board stays story-agnostic. */
export function battlePresentation(levelId: string): BattlePresentation {
  return PRESENTATIONS.find((presentation) => presentation.matches(levelId)) ?? GENERIC_PRESENTATION;
}
