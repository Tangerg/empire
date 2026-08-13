import environmentManifestJson from '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json';
import twinHillsSceneJson from '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/scenes/c01-01.scene.json';
import {
  EnvironmentCatalog,
  type EnvironmentManifest,
} from './environment-catalog';

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

const ENVIRONMENT_ROOT = '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/';
const environmentUrls = import.meta.glob<string>(
  [
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/surface-meadow.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/surface-meadow@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/surface-forest-floor.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/surface-forest-floor@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/transition-meadow-forest.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/transition-meadow-forest@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/route-dirt-road.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/route-dirt-road@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/route-edge-dirt-road.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/route-edge-dirt-road@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/cliff-modules-temperate.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/cliff-modules-temperate@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/forest-temperate.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/forest-temperate@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/camps-foundations.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/camps-foundations@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/decals-small.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/decals-small@2x.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/rural-life.png',
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/rural-life@2x.png',
  ],
  { eager: true, import: 'default', query: '?url' },
);

const selectedAtlasIds = new Set(twinHillsSceneJson.selection.allowAtlases);
const environmentManifest = environmentManifestJson as EnvironmentManifest;
export const CANDIDATE_01_ENVIRONMENT = new EnvironmentCatalog(
  { ...environmentManifest, atlases: environmentManifest.atlases.filter((atlas) => selectedAtlasIds.has(atlas.id)) },
  (relativePath) => environmentUrls[`${ENVIRONMENT_ROOT}${relativePath}`],
);

export const CANDIDATE_01_ENVIRONMENT_PACK = environmentManifestJson as EnvironmentManifest;

export const candidate01EnvironmentAtlas = (id: string) => CANDIDATE_01_ENVIRONMENT.atlas(id);

export const candidate01EnvironmentCell = (id: string) => CANDIDATE_01_ENVIRONMENT.cell(id);

export function environmentVariantIndex(
  mask: number,
  variantsPerMask: number,
  x: number,
  y: number,
  seed = 0,
): number {
  if (!Number.isInteger(mask) || mask < 0 || mask > 15) throw new Error(`invalid environment mask: ${mask}`);
  if (!Number.isInteger(variantsPerMask) || variantsPerMask < 1) throw new Error('variantsPerMask must be positive');
  const mixed = Math.imul(x ^ seed, 0x45d9f3b) ^ Math.imul(y + seed, 0x119de1f3);
  const variant = (mixed >>> 0) % variantsPerMask;
  return mask * variantsPerMask + variant;
}

export interface CandidateTerrainVisual {
  readonly atlas: ReturnType<typeof candidate01EnvironmentAtlas>;
  readonly cellIndex: number;
  readonly overlays: readonly {
    readonly atlas: ReturnType<typeof candidate01EnvironmentAtlas>;
    readonly cellIndex: number;
  }[];
}

export function resolveCandidate01TerrainVisual(
  atlasId: string,
  mask: number,
  x: number,
  y: number,
  seed = 0,
): CandidateTerrainVisual {
  if (!Number.isInteger(mask) || mask < 0 || mask > 15) throw new Error(`invalid environment mask: ${mask}`);
  const atlas = candidate01EnvironmentAtlas(atlasId);
  const cellIndex = environmentVariantIndex(mask, atlas.variantsPerMask ?? 1, x, y, seed);
  const edgeId = atlasId === 'route-dirt-road' ? 'route-edge-dirt-road' : undefined;
  return {
    atlas,
    cellIndex,
    overlays: edgeId ? [{ atlas: candidate01EnvironmentAtlas(edgeId), cellIndex }] : [],
  };
}

const scenes = new Map<string, CandidateEnvironmentScene>([
  [twinHillsSceneJson.levelId, twinHillsSceneJson as unknown as CandidateEnvironmentScene],
]);

export function candidate01EnvironmentScene(levelId: string): CandidateEnvironmentScene | undefined {
  return scenes.get(levelId);
}
