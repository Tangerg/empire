import document from '../../assets/final-fantasy-v1/manifest-hero-appearances.json';
import { candidate01Asset, candidate01TeamAsset, type Candidate01RuntimeAsset } from './candidate-01-assets';
import { CANDIDATE_01_UNIT_ART, type Candidate01UnitArtBinding } from './candidate-01-bindings';
import { fixedNumbers } from './candidate-01-documents';

interface HeroSheetDocument {
  readonly png: string;
  readonly hdPng: string;
  readonly pixelDensity: number;
  readonly hdWidth: number;
  readonly hdHeight: number;
  readonly frameWidth: number;
  readonly frameHeight: number;
  readonly frames: number;
  readonly frameOrder: readonly string[];
  readonly anchor: readonly number[];
}

export interface Candidate01HeroAppearanceDocument {
  readonly schemaVersion: number;
  readonly appearances: readonly (HeroSheetDocument & { readonly id: string; readonly topicId: string })[];
  readonly teamVariants: readonly (HeroSheetDocument & { readonly appearanceId: string; readonly teamColor: string })[];
}

export interface Candidate01HeroAppearanceCatalog {
  readonly appearances: ReadonlyMap<string, Candidate01RuntimeAsset>;
  readonly teamVariants: ReadonlyMap<string, Candidate01RuntimeAsset>;
}

const ROOT = '../../assets/final-fantasy-v1/';
const FRAME_ORDER = ['idle', 'walk-a', 'attack', 'walk-b'];
const SHEET_KEYS = ['png', 'hdPng', 'pixelDensity', 'hdWidth', 'hdHeight', 'frameWidth', 'frameHeight', 'frames', 'frameOrder', 'anchor'];

function exactFields(value: object, keys: readonly string[], subject: string): void {
  if (Object.keys(value).sort().join(',') !== [...keys].sort().join(',')) {
    throw new Error(`candidate-01 hero ${subject}: unsupported or missing fields`);
  }
}

function readSheet(base: Candidate01RuntimeAsset, raw: HeroSheetDocument, files: Readonly<Record<string, string>>, subject: string): Candidate01RuntimeAsset {
  if (base.category !== 'combat-unit' || base.frames !== 4
    || JSON.stringify(base.frameOrder) !== JSON.stringify(FRAME_ORDER)) {
    throw new Error(`candidate-01 hero ${subject}: base topic must be a four-frame combat unit`);
  }
  if (raw.frames !== 4 || JSON.stringify(raw.frameOrder) !== JSON.stringify(FRAME_ORDER)
    || !Number.isInteger(raw.frameWidth) || raw.frameWidth <= 0
    || !Number.isInteger(raw.frameHeight) || raw.frameHeight <= 0
    || raw.pixelDensity !== 2 || raw.hdWidth !== raw.frameWidth * raw.frames * 2
    || raw.hdHeight !== raw.frameHeight * 2) {
    throw new Error(`candidate-01 hero ${subject}: inconsistent frame/HD geometry or frame order`);
  }
  const anchor = fixedNumbers(2, raw.anchor, 'anchor', subject);
  if (anchor[0] < 0 || anchor[0] >= raw.frameWidth || anchor[1] < 0 || anchor[1] >= raw.frameHeight) {
    throw new Error(`candidate-01 hero ${subject}: pivot outside frame`);
  }
  const stem = /^runtime-approved-v5\/hero-appearance\/([a-z0-9]+(?:-[a-z0-9]+)*)\.png$/.exec(raw.png)?.[1];
  if (!stem || raw.hdPng !== `runtime-approved-v5/hero-appearance/${stem}@2x.png`) {
    throw new Error(`candidate-01 hero ${subject}: expected paired hero appearance runtime paths`);
  }
  const url = files[`${ROOT}${raw.hdPng}`];
  if (!url || !files[`${ROOT}${raw.png}`]) {
    throw new Error(`candidate-01 hero ${subject}: runtime sheet missing from Vite graph`);
  }
  // Canonical identity, category and footprint remain owned by the base asset.
  // The appearance owns only its registered sheet geometry and authored pixels.
  return { ...base, png: raw.png, url, anchor, frameWidth: raw.frameWidth, frameHeight: raw.frameHeight,
    width: raw.frameWidth * raw.frames, height: raw.frameHeight, frames: raw.frames,
    frameOrder: [...raw.frameOrder], preserveAuthoredPalette: true };
}

export function readCandidate01HeroAppearances(
  raw: Candidate01HeroAppearanceDocument,
  files: Readonly<Record<string, string>>,
): Candidate01HeroAppearanceCatalog {
  exactFields(raw, ['schemaVersion', 'appearances', 'teamVariants'], 'catalog');
  if (raw.schemaVersion !== 1) throw new Error('candidate-01 hero: unsupported appearance schema');
  const appearances = new Map<string, Candidate01RuntimeAsset>();
  const teamVariants = new Map<string, Candidate01RuntimeAsset>();
  const paths = new Set<string>();
  const register = (base: Candidate01RuntimeAsset, sheet: HeroSheetDocument, key: string) => {
    if (paths.has(sheet.png) || paths.has(sheet.hdPng)) throw new Error(`candidate-01 hero ${key}: duplicate runtime path`);
    const asset = readSheet(base, sheet, files, key);
    paths.add(sheet.png);
    paths.add(sheet.hdPng);
    return asset;
  };
  for (const appearance of raw.appearances) {
    exactFields(appearance, [...SHEET_KEYS, 'id', 'topicId'], appearance.id);
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(appearance.id) || appearances.has(appearance.id)) {
      throw new Error(`candidate-01 hero: invalid or duplicate appearance id ${appearance.id}`);
    }
    appearances.set(appearance.id, register(candidate01Asset(appearance.topicId), appearance, appearance.id));
  }
  for (const variant of raw.teamVariants) {
    exactFields(variant, [...SHEET_KEYS, 'appearanceId', 'teamColor'], variant.appearanceId);
    const base = appearances.get(variant.appearanceId);
    if (!base) throw new Error(`candidate-01 hero: unknown team-variant appearance ${variant.appearanceId}`);
    const key = `${variant.appearanceId}:${variant.teamColor.toLowerCase()}`;
    if (!/^#[0-9a-f]{6}$/i.test(variant.teamColor) || teamVariants.has(key)) {
      throw new Error(`candidate-01 hero: invalid or duplicate team variant ${key}`);
    }
    teamVariants.set(key, register(base, variant, key));
  }
  return { appearances, teamVariants };
}

/** Named identity wins over generic team art; the team ring still conveys ownership. */
export function resolveCandidate01UnitArt(
  catalog: Candidate01HeroAppearanceCatalog,
  binding: Candidate01UnitArtBinding,
  teamColor: string,
): Candidate01RuntimeAsset {
  if (binding.appearanceId === undefined) return candidate01TeamAsset(binding.topicId, teamColor);
  const base = catalog.appearances.get(binding.appearanceId);
  if (!base) throw new Error(`candidate-01 hero: unknown bound appearance ${binding.appearanceId}`);
  if (base.topicId !== binding.topicId) throw new Error(`candidate-01 hero: bound appearance topic mismatch ${binding.appearanceId}`);
  return catalog.teamVariants.get(`${binding.appearanceId}:${teamColor.toLowerCase()}`) ?? base;
}

const runtimeFiles = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/runtime-approved-v5/hero-appearance/*.png',
  { eager: true, import: 'default', query: '?url' },
);
export const CANDIDATE_01_HERO_APPEARANCES = readCandidate01HeroAppearances(document, runtimeFiles);
// A broken explicit binding is a startup error even if that unit is absent from the current mission.
for (const binding of Object.values(CANDIDATE_01_UNIT_ART)) {
  if (!binding) throw new Error('candidate-01 hero: undefined unit art binding');
  resolveCandidate01UnitArt(CANDIDATE_01_HERO_APPEARANCES, binding, '');
}
