import environmentManifestJson from '../../assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json';
import twinHillsSceneJson from '../../assets/final-fantasy-v1/environment-builder-v1/scenes/c01-01.scene.json';
import redstoneSceneJson from '../../assets/final-fantasy-v1/environment-builder-v1/scenes/c01-03.scene.json';
import blackCampSceneJson from '../../assets/final-fantasy-v1/environment-builder-v1/scenes/c01-04.scene.json';
import oldBannerSceneJson from '../../assets/final-fantasy-v1/environment-builder-v1/scenes/c01-10.scene.json';
import cemeterySceneJson from '../../assets/final-fantasy-v1/environment-builder-v1/scenes/c01-11.scene.json';
import silverwoodSceneJson from '../../assets/final-fantasy-v1/environment-builder-v1/scenes/c01-15.scene.json';
import {
  EnvironmentCatalog,
  type EnvironmentManifest,
} from '@empire/game-ui';
import type { CandidateRoadCorridorProfile } from './candidate-01-road-corridor';
import { fixedNumbers, oneOf, type FixedNumbers } from './candidate-01-documents';

/** The four depths an authored placement may sit at, as values and as a type. */
export const PLACEMENT_LAYERS = ['foundation', 'ground-decal', 'under-units', 'over-units'] as const;

export interface CandidateEnvironmentPlacement {
  readonly id?: string;
  readonly topicId?: string;
  readonly x: number;
  readonly y: number;
  readonly layer?: (typeof PLACEMENT_LAYERS)[number];
  readonly variant?: number;
  readonly scale?: number;
  readonly flip?: boolean;
  readonly opacity?: number;
  readonly standingShadow?: boolean;
}

export interface CandidateEnvironmentScene {
  readonly schemaVersion: string;
  readonly levelId: string;
  readonly runtimeReady: boolean;
  readonly roadCorridor?: CandidateRoadCorridorProfile;
  readonly mapSize: FixedNumbers<2>;
  readonly zones: readonly {
    readonly id: string;
    readonly bounds: FixedNumbers<4>;
    readonly role: string;
    readonly authoredTerrainScenery?: readonly string[];
    readonly authoredTerrainSurface?: readonly string[];
  }[];
  readonly selection: {
    readonly allowAtlases: readonly string[];
    readonly denyAtlases?: readonly string[];
  };
  readonly placements: readonly CandidateEnvironmentPlacement[];
  readonly groundOutline?: readonly FixedNumbers<2>[];
  readonly framePlacements?: readonly {
    readonly id: string;
    readonly contact: FixedNumbers<2>;
    readonly scale: number;
    readonly flip: boolean;
  }[];
  readonly populationBindings?: readonly Readonly<Record<string, unknown>>[];
}

const ENVIRONMENT_ROOT = '../../assets/final-fantasy-v1/environment-builder-v1/';

/**
 * Every atlas the kit ships, at both densities.
 *
 * This was a list of ten atlases spelled out twice — once as glob patterns and
 * once as `twinHillsSceneJson.selection.allowAtlases`, which is the asset
 * allowlist inside *chapter one's authored scene document*. So the pack's catalog
 * was one map's dressing list, and the other twenty-six atlases — the waters, the
 * other three routes, the earth and stone and snow and graveyard surfaces, the
 * crossings, the landmarks — could not be reached by any level at all.
 *
 * The manifest is the catalog. It costs 2 MB of atlas over the ten (17 MB against
 * 15 MB), and it buys every terrain in the campaign a material.
 */
const environmentUrls = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/*.png',
  { eager: true, import: 'default', query: '?url' },
);

// No assertion: the manifest's shape is a document shape, and the catalog checks
// the parts of it a JSON import cannot state.
const environmentManifest: EnvironmentManifest = environmentManifestJson;
export const CANDIDATE_01_ENVIRONMENT = new EnvironmentCatalog(
  environmentManifest,
  (relativePath) => environmentUrls[`${ENVIRONMENT_ROOT}${relativePath}`],
);

type SceneDocument = Omit<CandidateEnvironmentScene, 'mapSize' | 'zones' | 'placements' | 'roadCorridor' | 'framePlacements' | 'groundOutline'> & {
  readonly mapSize: readonly number[];
  readonly zones: readonly (Omit<CandidateEnvironmentScene['zones'][number], 'bounds'> & { readonly bounds: readonly number[] })[];
  readonly placements: readonly (Omit<CandidateEnvironmentPlacement, 'layer'> & { readonly layer?: string })[];
  readonly roadCorridor?: Omit<CandidateRoadCorridorProfile, 'protectedCells'> & { readonly protectedCells: readonly (readonly number[])[] };
  readonly framePlacements?: readonly (Omit<NonNullable<CandidateEnvironmentScene['framePlacements']>[number], 'contact'> & { readonly contact: readonly number[] })[];
  readonly groundOutline?: readonly (readonly number[])[];
};

/** An authored scene, read rather than asserted. See `candidate-01-documents.ts`. */
function readScene(document: SceneDocument): CandidateEnvironmentScene {
  const subject = document.levelId;
  const { roadCorridor, framePlacements, groundOutline, ...required } = document;
  return {
    ...required,
    ...(roadCorridor ? { roadCorridor: {
      ...roadCorridor,
      protectedCells: roadCorridor.protectedCells.map(point => fixedNumbers(2, point, 'protected road cell', subject)),
    } } : {}),
    mapSize: fixedNumbers(2, document.mapSize, 'mapSize', subject),
    zones: document.zones.map((zone) => ({
      ...zone,
      bounds: fixedNumbers(4, zone.bounds, 'bounds', `${subject} zone ${zone.id}`),
    })),
    placements: document.placements.map((placement) => {
      const { layer, ...rest } = placement;
      return {
        ...rest,
        ...(layer === undefined
          ? {}
          : { layer: oneOf(PLACEMENT_LAYERS, layer, 'layer', `${subject} placement ${rest.id ?? rest.topicId}`) }),
      };
    }),
    ...(framePlacements ? { framePlacements: framePlacements.map((placement) => ({
      ...placement,
      contact: fixedNumbers(2, placement.contact, 'contact', `${subject} frame ${placement.id}`),
    })) } : {}),
    ...(groundOutline ? { groundOutline: groundOutline.map((point) => fixedNumbers(2, point, 'ground outline point', subject)) } : {}),
  };
}

const scenes = new Map<string, CandidateEnvironmentScene>([
  [twinHillsSceneJson.levelId, readScene(twinHillsSceneJson)],
  [redstoneSceneJson.levelId, readScene(redstoneSceneJson)],
  [blackCampSceneJson.levelId, readScene(blackCampSceneJson)],
  [oldBannerSceneJson.levelId, readScene(oldBannerSceneJson)],
  [silverwoodSceneJson.levelId, readScene(silverwoodSceneJson)],
  [cemeterySceneJson.levelId, readScene(cemeterySceneJson)],
]);

export function candidate01EnvironmentScene(levelId: string): CandidateEnvironmentScene | undefined {
  return scenes.get(levelId);
}
