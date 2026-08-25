import { fixedNumbers, oneOf } from './candidate-01-documents';
import manifestJson from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import tacticalHdManifestJson from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';

/*
 * The closed sets this manifest is allowed to say, stated once as values.
 *
 * They used to be unions written in the type only, and the manifest was read with
 * `as unknown as Candidate01AssetManifest` — which is the one way that assignment
 * compiles, because a JSON import gives `category: string`. So the twelve names
 * were checked against nothing: a generator that emitted `terrian` typechecked,
 * and the asset simply never appeared. Derive the union from the list and the
 * list is both the type and the check. See `candidate-01-documents.ts`.
 */
const ASSET_CATEGORIES = [
  'narrative-static',
  'combat-unit',
  'mission-unit',
  'faction-kit',
  'terrain',
  'interactive-structure',
  'battle-prop',
  'equipment',
  'skill',
  'status',
  'fx',
  'hud',
] as const;
const ASSET_KINDS = ['scene', 'character-card', 'key-prop'] as const;
const TILE_MODES = ['variants', 'nesw-16'] as const;

export type Candidate01AssetCategory = (typeof ASSET_CATEGORIES)[number];

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
  kind?: (typeof ASSET_KINDS)[number];
  frameWidth?: number;
  frameHeight?: number;
  frames?: number;
  frameOrder?: string[];
  stateOrder?: string[];
  anchor?: readonly [number, number];
  footprint?: readonly [number, number];
  tileWidth?: number;
  tileHeight?: number;
  variants?: number;
  connectionMasks?: number;
  tileMode?: (typeof TILE_MODES)[number];
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

/**
 * The generated manifest, read rather than asserted.
 *
 * Everything the compiler *can* check against the JSON's own inferred type it
 * does: a record that loses `width`, or gains a field the record type has no
 * place for, is a build error. What it cannot check is what the assertion used to
 * cover — three closed sets of names and two pairs of numbers — so those are
 * checked here, once, at the edge where the document arrives.
 */
function assetRecord(raw: (typeof manifestJson)['assets'][number]): Candidate01AssetRecord {
  // Taken out of the spread rather than overridden in it: `exactOptionalPropertyTypes`
  // is what makes "absent" and "present as undefined" different answers here.
  const { category, kind, tileMode, anchor, footprint, ...rest } = raw;
  return {
    ...rest,
    category: oneOf(ASSET_CATEGORIES, category, 'category', raw.assetId),
    ...(anchor === undefined ? {} : { anchor: fixedNumbers(2, anchor, 'anchor', raw.assetId) }),
    ...(footprint === undefined ? {} : { footprint: fixedNumbers(2, footprint, 'footprint', raw.assetId) }),
    ...(kind === undefined ? {} : { kind: oneOf(ASSET_KINDS, kind, 'kind', raw.assetId) }),
    ...(tileMode === undefined
      ? {}
      : { tileMode: oneOf(TILE_MODES, tileMode, 'tileMode', raw.assetId) }),
  };
}

const manifest: Candidate01AssetManifest = {
  ...manifestJson,
  assets: manifestJson.assets.map(assetRecord),
};
const runtimeFiles = import.meta.glob<string>(
  [
    '../../assets/final-fantasy-v1/runtime/**/*.png',
    '!../../assets/final-fantasy-v1/runtime/{combat-unit,mission-unit,terrain,interactive-structure,battle-prop,fx}/*.png',
  ],
  { eager: true, import: 'default', query: '?url' },
);
const tacticalHdFiles = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/runtime-hd/**/*.png',
  { eager: true, import: 'default', query: '?url' },
);
const packRoot = '../../assets/final-fantasy-v1/';
// No assertion needed: the generated JSON already satisfies this shape.
const tacticalHdManifest: Candidate01TacticalHdManifest = tacticalHdManifestJson;
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

export function candidate01Asset(topicId: string): Candidate01RuntimeAsset {
  const record = catalog.get(topicId);
  if (!record) throw new Error(`unknown candidate-01 art topic: ${topicId}`);
  return record;
}

/** Does this pack draw that topic at all? A question, so it answers null. */
export function candidate01TryAsset(topicId: string): Candidate01RuntimeAsset | null {
  return catalog.get(topicId) ?? null;
}

export function candidate01AssetUrl(topicId: string): string {
  return candidate01Asset(topicId).url;
}

