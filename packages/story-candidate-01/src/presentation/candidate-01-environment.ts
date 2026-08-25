import environmentManifestJson from '../../assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json';
import twinHillsSceneJson from '../../assets/final-fantasy-v1/environment-builder-v1/scenes/c01-01.scene.json';
import {
  EnvironmentCatalog,
  type EnvironmentManifest,
} from '@empire/game-ui';

export interface CandidateEnvironmentPlacement {
  readonly id?: string;
  readonly topicId?: string;
  readonly x: number;
  readonly y: number;
  readonly layer?: 'foundation' | 'ground-decal' | 'under-units' | 'over-units';
  readonly variant?: number;
  readonly scale?: number;
  readonly flip?: boolean;
  readonly opacity?: number;
}

export interface CandidateEnvironmentScene {
  readonly schemaVersion: string;
  readonly levelId: string;
  readonly runtimeReady: boolean;
  readonly mapSize: readonly [number, number];
  readonly zones: readonly {
    readonly id: string;
    readonly bounds: readonly [number, number, number, number];
    readonly role: string;
  }[];
  readonly selection: {
    readonly allowAtlases: readonly string[];
    readonly denyAtlases?: readonly string[];
  };
  readonly placements: readonly CandidateEnvironmentPlacement[];
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

const environmentManifest = environmentManifestJson as EnvironmentManifest;
export const CANDIDATE_01_ENVIRONMENT = new EnvironmentCatalog(
  environmentManifest,
  (relativePath) => environmentUrls[`${ENVIRONMENT_ROOT}${relativePath}`],
);

const scenes = new Map<string, CandidateEnvironmentScene>([
  [twinHillsSceneJson.levelId, twinHillsSceneJson as unknown as CandidateEnvironmentScene],
]);

export function candidate01EnvironmentScene(levelId: string): CandidateEnvironmentScene | undefined {
  return scenes.get(levelId);
}
