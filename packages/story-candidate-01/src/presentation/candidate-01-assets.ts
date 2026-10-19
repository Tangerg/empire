import { fixedNumbers, oneOf } from './candidate-01-documents';
import manifestJson from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import teamVariantsJson from '../../assets/final-fantasy-v1/manifest-authored-team-variants.json';
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
  presentationScale?: number;
  preserveAuthoredPalette?: boolean;
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
  assets: Array<{ topicId: string; png: string; width: number; height: number; pixelDensity: number }>;
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
export function readCandidate01AssetRecord(raw: (typeof manifestJson)['assets'][number]): Candidate01AssetRecord {
  // Taken out of the spread rather than overridden in it: `exactOptionalPropertyTypes`
  // is what makes "absent" and "present as undefined" different answers here.
  const { category, kind, tileMode, anchor, footprint, ...rest } = raw;
  const record: Candidate01AssetRecord = {
    ...rest,
    category: oneOf(ASSET_CATEGORIES, category, 'category', raw.assetId),
    ...(anchor === undefined ? {} : { anchor: fixedNumbers(2, anchor, 'anchor', raw.assetId) }),
    ...(footprint === undefined ? {} : { footprint: fixedNumbers(2, footprint, 'footprint', raw.assetId) }),
    ...(kind === undefined ? {} : { kind: oneOf(ASSET_KINDS, kind, 'kind', raw.assetId) }),
    ...(tileMode === undefined
      ? {}
      : { tileMode: oneOf(TILE_MODES, tileMode, 'tileMode', raw.assetId) }),
  };
  if (record.presentationScale !== undefined && (!Number.isFinite(record.presentationScale) || record.presentationScale <= 0)) {
    throw new Error(`candidate-01: "${record.topicId}" has invalid presentation scale`);
  }
  if (record.frameWidth !== undefined || record.frameHeight !== undefined || record.frames !== undefined) {
    const { frameWidth, frameHeight, frames } = record;
    if (
      frameWidth === undefined || !Number.isInteger(frameWidth) || frameWidth < 1
      || frameHeight === undefined || !Number.isInteger(frameHeight) || frameHeight < 1
      || frames === undefined || !Number.isInteger(frames) || frames < 1
      || record.width !== frameWidth * frames || record.height !== frameHeight
    ) {
      throw new Error(`candidate-01: "${record.topicId}" has inconsistent frame dimensions`);
    }
    const order = record.frameOrder ?? record.stateOrder;
    if (order && (order.length !== frames || new Set(order).size !== frames)) {
      throw new Error(`candidate-01: "${record.topicId}" has inconsistent frame order`);
    }
  }
  return record;
}

const manifest: Candidate01AssetManifest = {
  ...manifestJson,
  assets: manifestJson.assets.map(readCandidate01AssetRecord),
};
const runtimeFiles = import.meta.glob<string>(
  [
    '../../assets/final-fantasy-v1/runtime/**/*.png',
    '!../../assets/final-fantasy-v1/runtime/{combat-unit,mission-unit,terrain,interactive-structure,battle-prop,fx}/*.png',
  ],
  { eager: true, import: 'default', query: '?url' },
);
const tacticalHdFiles = import.meta.glob<string>(
  [
    '../../assets/final-fantasy-v1/runtime-hd/**/*.png',
    '../../assets/final-fantasy-v1/runtime-approved-v5/**/*@2x.png',
  ],
  { eager: true, import: 'default', query: '?url' },
);
const packRoot = '../../assets/final-fantasy-v1/';
// No assertion needed: the generated JSON already satisfies this shape.
const tacticalHdManifest: Candidate01TacticalHdManifest = tacticalHdManifestJson;
const tacticalHdTopics = new Map(
  tacticalHdManifest.assets.map((record) => [record.topicId, record] as const),
);

if (
  tacticalHdManifest.pixelDensity < 2
  || tacticalHdManifest.assetCount !== tacticalHdManifest.assets.length
  || tacticalHdManifest.assetCount !== tacticalHdTopics.size
) {
  throw new Error('candidate-01 tactical HD manifest is incomplete or contains duplicate topic ids');
}

function resolveRuntimeUrl(record: Candidate01AssetRecord): string {
  const hd = tacticalHdTopics.get(record.topicId);
  if (hd) {
    if (
      hd.pixelDensity !== tacticalHdManifest.pixelDensity
      || hd.width !== record.width * hd.pixelDensity
      || hd.height !== record.height * hd.pixelDensity
    ) {
      throw new Error(`candidate-01 tactical HD dimensions disagree with the logical asset: ${record.topicId}`);
    }
    const hdUrl = tacticalHdFiles[`${packRoot}${hd.png}`];
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


/** Authored palette variants reuse the same gameplay topic; colour selection is explicit art metadata. */
const teamVariants = new Map<string, Candidate01RuntimeAsset>();
if (teamVariantsJson.schemaVersion !== 1) throw new Error('unsupported authored team variant schema');
for (const variant of teamVariantsJson.variants) {
  const base = candidate01Asset(variant.topicId);
  const key = `${variant.topicId}:${variant.teamColor.toLowerCase()}`;
  if (!/^#[0-9a-f]{6}$/i.test(variant.teamColor) || teamVariants.has(key)) {
    throw new Error(`invalid or duplicate authored team variant: ${key}`);
  }
  if (variant.pixelDensity !== 2 || variant.frames !== base.frames
    || JSON.stringify(variant.frameOrder) !== JSON.stringify(base.frameOrder)
    || !Number.isInteger(variant.frameWidth) || variant.frameWidth < 1
    || !Number.isInteger(variant.frameHeight) || variant.frameHeight < 1) {
    throw new Error(`inconsistent authored team variant geometry: ${key}`);
  }
  const anchor = fixedNumbers(2, variant.anchor, 'anchor', key);
  if (anchor[0] < 0 || anchor[0] > variant.frameWidth || anchor[1] < 0 || anchor[1] > variant.frameHeight) {
    throw new Error(`authored team variant pivot outside frame: ${key}`);
  }
  const url = tacticalHdFiles[`${packRoot}${variant.hdPng}`];
  if (!url) throw new Error(`authored team variant missing from Vite graph: ${key}`);
  teamVariants.set(key, { ...base, png: variant.png, url, anchor,
    width: variant.frameWidth * variant.frames, height: variant.frameHeight,
    frameWidth: variant.frameWidth, frameHeight: variant.frameHeight });
}

export function candidate01TeamAsset(topicId: string, teamColor: string): Candidate01RuntimeAsset {
  return teamVariants.get(`${topicId}:${teamColor.toLowerCase()}`) ?? candidate01Asset(topicId);
}
