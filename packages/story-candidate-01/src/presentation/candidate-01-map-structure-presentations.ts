import document from '../../assets/final-fantasy-v1/manifest-map-structure-presentations.json';
import { candidate01Asset, type Candidate01RuntimeAsset } from './candidate-01-assets';
import { CANDIDATE_01_MAP_STRUCTURE_ART, type Candidate01MapStructureArtBinding } from './candidate-01-bindings';
import { fixedNumbers } from './candidate-01-documents';

export interface Candidate01MapStructurePresentationDocument {
  readonly schemaVersion: number;
  readonly variants: readonly {
    readonly id: string;
    readonly topicId: string;
    readonly png: string;
    readonly hdPng: string;
    readonly pixelDensity: number;
    readonly hdWidth: number;
    readonly hdHeight: number;
    readonly frameWidth: number;
    readonly frameHeight: number;
    readonly frames: number;
    readonly stateOrder: readonly string[];
    readonly anchor: readonly number[];
    readonly footprint: readonly number[];
    readonly presentationScale: number;
  }[];
}

const ROOT = '../../assets/final-fantasy-v1/';
const STATES = ['normal', 'damaged', 'captured'];
const VARIANT_KEYS = ['id', 'topicId', 'png', 'hdPng', 'pixelDensity', 'hdWidth', 'hdHeight',
  'frameWidth', 'frameHeight', 'frames', 'stateOrder', 'anchor', 'footprint', 'presentationScale'];

function exactFields(value: object, keys: readonly string[], subject: string): void {
  if (Object.keys(value).sort().join(',') !== [...keys].sort().join(',')) {
    throw new Error(`candidate-01 map structure ${subject}: unsupported or missing fields`);
  }
}

/** A role-specific sheet keeps its canonical identity while owning its complete visual geometry. */
export function readCandidate01MapStructurePresentations(
  raw: Candidate01MapStructurePresentationDocument,
  files: Readonly<Record<string, string>>,
): ReadonlyMap<string, Candidate01RuntimeAsset> {
  exactFields(raw, ['schemaVersion', 'variants'], 'catalog');
  if (raw.schemaVersion !== 1) throw new Error('candidate-01 map structure: unsupported presentation schema');
  const variants = new Map<string, Candidate01RuntimeAsset>();
  const paths = new Set<string>();
  for (const variant of raw.variants) {
    const subject = variant.id;
    exactFields(variant, VARIANT_KEYS, subject);
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(subject) || variants.has(subject)) {
      throw new Error(`candidate-01 map structure: invalid or duplicate presentation id ${subject}`);
    }
    const base = candidate01Asset(variant.topicId);
    if (base.category !== 'interactive-structure' || base.frames !== 3
      || JSON.stringify(base.stateOrder) !== JSON.stringify(STATES)) {
      throw new Error(`candidate-01 map structure ${subject}: base topic must be a three-state interactive structure`);
    }
    if (variant.frames !== 3 || JSON.stringify(variant.stateOrder) !== JSON.stringify(STATES)
      || !Number.isInteger(variant.frameWidth) || variant.frameWidth <= 0
      || !Number.isInteger(variant.frameHeight) || variant.frameHeight <= 0
      || variant.pixelDensity !== 2 || variant.hdWidth !== variant.frameWidth * variant.frames * 2
      || variant.hdHeight !== variant.frameHeight * 2) {
      throw new Error(`candidate-01 map structure ${subject}: inconsistent frame/HD geometry or state order`);
    }
    const anchor = fixedNumbers(2, variant.anchor, 'anchor', subject);
    if (anchor[0] < 0 || anchor[0] >= variant.frameWidth || anchor[1] < 0 || anchor[1] >= variant.frameHeight) {
      throw new Error(`candidate-01 map structure ${subject}: pivot outside frame`);
    }
    const footprint = fixedNumbers(2, variant.footprint, 'footprint', subject);
    if (footprint.some(value => !Number.isInteger(value) || value <= 0)) {
      throw new Error(`candidate-01 map structure ${subject}: invalid visual footprint`);
    }
    if (!Number.isFinite(variant.presentationScale) || variant.presentationScale <= 0) {
      throw new Error(`candidate-01 map structure ${subject}: invalid presentation scale`);
    }
    const stem = /^runtime-approved-v5\/map-structure-presentation\/([a-z0-9]+(?:-[a-z0-9]+)*)\.png$/.exec(variant.png)?.[1];
    if (!stem || variant.hdPng !== `runtime-approved-v5/map-structure-presentation/${stem}@2x.png`) {
      throw new Error(`candidate-01 map structure ${subject}: expected paired presentation runtime paths`);
    }
    if (paths.has(variant.png) || paths.has(variant.hdPng)) {
      throw new Error(`candidate-01 map structure ${subject}: duplicate runtime path`);
    }
    const url = files[`${ROOT}${variant.hdPng}`];
    if (!url || !files[`${ROOT}${variant.png}`]) {
      throw new Error(`candidate-01 map structure ${subject}: runtime sheet missing from Vite graph`);
    }
    paths.add(variant.png);
    paths.add(variant.hdPng);
    variants.set(subject, { ...base, png: variant.png, url, anchor, footprint,
      width: variant.frameWidth * variant.frames, height: variant.frameHeight,
      frameWidth: variant.frameWidth, frameHeight: variant.frameHeight, frames: variant.frames,
      stateOrder: [...variant.stateOrder], presentationScale: variant.presentationScale });
  }
  return variants;
}

export function resolveCandidate01MapStructureArt(
  variants: ReadonlyMap<string, Candidate01RuntimeAsset>,
  binding: Candidate01MapStructureArtBinding,
): Candidate01RuntimeAsset {
  if (binding.presentationId === undefined) return candidate01Asset(binding.topicId);
  const variant = variants.get(binding.presentationId);
  if (!variant) throw new Error(`candidate-01 map structure: unknown bound presentation ${binding.presentationId}`);
  if (variant.topicId !== binding.topicId) {
    throw new Error(`candidate-01 map structure: bound presentation topic mismatch ${binding.presentationId}`);
  }
  return variant;
}

const runtimeFiles = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/runtime-approved-v5/map-structure-presentation/*.png',
  { eager: true, import: 'default', query: '?url' },
);
export const CANDIDATE_01_MAP_STRUCTURE_PRESENTATIONS = readCandidate01MapStructurePresentations(document, runtimeFiles);
for (const binding of Object.values(CANDIDATE_01_MAP_STRUCTURE_ART)) {
  if (!binding) throw new Error('candidate-01 map structure: undefined art binding');
  resolveCandidate01MapStructureArt(CANDIDATE_01_MAP_STRUCTURE_PRESENTATIONS, binding);
}
