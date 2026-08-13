import manifestJson from '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import tacticalHdManifestJson from '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';

export type Candidate01AssetCategory =
  | 'narrative-static'
  | 'combat-unit'
  | 'mission-unit'
  | 'faction-kit'
  | 'terrain'
  | 'interactive-structure'
  | 'battle-prop'
  | 'equipment'
  | 'skill'
  | 'status'
  | 'fx'
  | 'hud';

export interface Candidate01AssetRecord {
  assetId: string;
  topicId: string;
  label: string;
  category: Candidate01AssetCategory;
  png: string;
  width: number;
  height: number;
  runtimeReady: boolean;
  qualityTier: string;
  kind?: 'scene' | 'character-card' | 'key-prop';
  frameWidth?: number;
  frameHeight?: number;
  frames?: number;
  frameOrder?: string[];
  stateOrder?: string[];
  anchor?: [number, number];
  footprint?: [number, number];
  tileWidth?: number;
  tileHeight?: number;
  variants?: number;
  connectionMasks?: number;
  tileMode?: 'variants' | 'nesw-16';
  fps?: number;
  loop?: boolean;
  blendMode?: string;
}

interface Candidate01AssetManifest {
  schemaVersion: string;
  campaignId: string;
  assetCount: number;
  runtimeReady: boolean;
  assets: Candidate01AssetRecord[];
}

interface Candidate01TacticalHdManifest {
  schemaVersion: string;
  pixelDensity: number;
  assetCount: number;
  assets: Array<{ topicId: string; png: string }>;
}

export interface Candidate01RuntimeAsset extends Candidate01AssetRecord {
  url: string;
}

const manifest = manifestJson as unknown as Candidate01AssetManifest;
const runtimeFiles = import.meta.glob<string>(
  [
    '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/runtime/**/*.png',
    '!../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/runtime/{combat-unit,mission-unit,terrain,interactive-structure,battle-prop,fx}/*.png',
  ],
  { eager: true, import: 'default', query: '?url' },
);
const tacticalHdFiles = import.meta.glob<string>(
  '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/runtime-hd/**/*.png',
  { eager: true, import: 'default', query: '?url' },
);
const packRoot = '../../docs/story-candidates/candidate-01/assets/final-fantasy-v1/';
const tacticalHdManifest = tacticalHdManifestJson as unknown as Candidate01TacticalHdManifest;
const tacticalHdTopics = new Map(
  tacticalHdManifest.assets.map((record) => [record.topicId, record.png] as const),
);

if (
  tacticalHdManifest.pixelDensity < 2
  || tacticalHdManifest.assetCount !== tacticalHdManifest.assets.length
  || tacticalHdManifest.assetCount !== tacticalHdTopics.size
) {
  throw new Error('candidate-01 tactical HD manifest is incomplete or contains duplicate topic ids');
}

function resolveRuntimeUrl(record: Candidate01AssetRecord): string {
  const hdPath = tacticalHdTopics.get(record.topicId);
  if (hdPath) {
    const hdUrl = tacticalHdFiles[`${packRoot}${hdPath}`];
    if (!hdUrl) throw new Error(`candidate-01 tactical HD asset is missing from the Vite graph: ${record.topicId}`);
    return hdUrl;
  }
  const url = runtimeFiles[`${packRoot}${record.png}`];
  if (!url) throw new Error(`candidate-01 asset is missing from the Vite graph: ${record.topicId} (${record.png})`);
  return url;
}

const catalog = new Map<string, Candidate01RuntimeAsset>();
for (const record of manifest.assets) {
  if (catalog.has(record.topicId)) throw new Error(`duplicate candidate-01 topicId: ${record.topicId}`);
  catalog.set(record.topicId, { ...record, url: resolveRuntimeUrl(record) });
}

if (!manifest.runtimeReady || manifest.assetCount !== catalog.size) {
  throw new Error(`candidate-01 asset manifest is not runtime ready (${catalog.size}/${manifest.assetCount})`);
}

/** The versioned art catalog is presentation-only; combat content keeps stable engine ids. */
export const CANDIDATE_01_ASSET_PACK = Object.freeze({
  id: 'candidate-01-cartoon-fantasy-v1',
  schemaVersion: manifest.schemaVersion,
  assetCount: manifest.assetCount,
  tacticalHdAssetCount: tacticalHdManifest.assetCount,
  tacticalPixelDensity: tacticalHdManifest.pixelDensity,
});

export function candidate01Asset(topicId: string): Candidate01RuntimeAsset {
  const record = catalog.get(topicId);
  if (!record) throw new Error(`unknown candidate-01 art topic: ${topicId}`);
  return record;
}

export function candidate01AssetUrl(topicId: string): string {
  return candidate01Asset(topicId).url;
}

export function candidate01Assets(category: Candidate01AssetCategory): readonly Candidate01RuntimeAsset[] {
  return [...catalog.values()].filter((record) => record.category === category);
}
