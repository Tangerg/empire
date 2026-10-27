import { readFileSync, writeFileSync, mkdirSync, copyFileSync, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { createHash } from 'node:crypto';
import { CANDIDATE_01_ART } from '../../packages/story-candidate-01/src/presentation/index';
import { CANDIDATE_01_WEAPON_ART, CANDIDATE_01_ABILITY_ART, CANDIDATE_01_STATUS_ART } from '../../packages/story-candidate-01/src/presentation/candidate-01-bindings';
import { candidate01AssetUrl } from '../../packages/story-candidate-01/src/presentation/candidate-01-assets';
import { objectFields, readNativePortraitCatalog, requirePortraitId } from './portrait-catalog';

const catalog = readNativePortraitCatalog(resolve('godot'));
const nativePortraitFiles = new Set([...catalog.values()].map(asset => resolve('godot', asset.resourcePath.slice('res://'.length))));
const selected = JSON.parse(readFileSync('godot/data/selected-assets.json', 'utf8')).assets as Array<{
  id?: string; topicId: string; frameOrder?: string[]; frameRegions?: number[][];
  frameWidth?: number; frameHeight?: number; width?: number; height?: number; pixelDensity: number;
}>;
const artBindings = JSON.parse(readFileSync('godot/data/art-bindings.json', 'utf8'));
const metadataPath = 'godot/data/tactical-hud-art.json';
const metadata = objectFields(JSON.parse(readFileSync(metadataPath, 'utf8')),
  ['schema', 'portraits', 'icons', 'sourceIconKeys'], 'native tactical HUD');
if (metadata.schema !== 2 || !metadata.portraits || typeof metadata.portraits !== 'object' || Array.isArray(metadata.portraits)) {
  throw new Error('Native tactical HUD requires schema 2 with explicit portrait bindings');
}
for (const [key, value] of Object.entries(metadata.portraits)) {
  const binding = objectFields(value, ['unitType', 'actorKey', 'portraitId', 'portraitRegion'], `HUD portrait binding ${key}`);
  if (typeof binding.unitType !== 'string' || !binding.unitType
    || !(binding.actorKey === null || (typeof binding.actorKey === 'string' && binding.actorKey.length > 0))
    || key !== (binding.actorKey ?? binding.unitType)) {
    throw new Error(`Invalid native HUD portrait binding: ${key}`);
  }
  // Null explicitly selects the bound unit's idle frame, not a missing named card.
  if (binding.portraitId !== null) {
    requirePortraitId(binding.portraitId, catalog, `HUD binding ${key}`);
    const crop = binding.portraitRegion;
    const portrait = catalog.get(binding.portraitId as string)!;
    const png = readFileSync(resolve('godot', portrait.resourcePath.slice('res://'.length)));
    if (!png.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))) throw new Error(`HUD portrait is not a PNG: ${key}`);
    if (!Array.isArray(crop) || crop.length !== 4 || !crop.every(Number.isSafeInteger)
      || crop[0] < 0 || crop[1] < 0 || crop[2] <= 0 || crop[2] !== crop[3]
      || crop[0] + crop[2] > png.readUInt32BE(16) || crop[1] + crop[3] > png.readUInt32BE(20)) {
      throw new Error(`HUD portrait requires an in-bounds square physical-pixel crop: ${key}`);
    }
  } else if (binding.portraitRegion !== null) {
    const actor = binding.actorKey === null ? null : artBindings.CANDIDATE_01_ACTOR_ART[binding.actorKey as string];
    const unit = actor?.unitArt ?? artBindings.CANDIDATE_01_UNIT_ART[binding.unitType];
    const asset = selected.find(asset => unit?.appearanceId ? asset.id === unit.appearanceId : !asset.id && asset.topicId === unit?.topicId);
    const idle = asset?.frameOrder?.indexOf('idle') ?? -1;
    if (!asset || idle < 0) throw new Error(`Missing generic idle-frame source: ${key}`);
    const region = asset.frameRegions?.[idle];
    const width = region?.[2] ?? (asset.frameWidth ?? asset.width ?? 0) * asset.pixelDensity;
    const height = region?.[3] ?? (asset.frameHeight ?? asset.height ?? 0) * asset.pixelDensity;
    const crop = binding.portraitRegion;
    if (!Array.isArray(crop) || crop.length !== 4 || !crop.every(Number.isSafeInteger)
      || crop[0] < 0 || crop[1] < 0 || crop[2] <= 0 || crop[2] !== crop[3]
      || crop[0] + crop[2] > width || crop[1] + crop[3] > height)
      throw new Error(`Generic HUD portrait requires an in-bounds square idle-frame-local crop: ${key}`);
  }
}

const prefix = '/packages/story-candidate-01/assets/final-fantasy-v1/';
function image(source: string) {
  if (!source.startsWith(prefix)) throw new Error('Unknown HUD icon source ' + source);
  const relative = source.slice(prefix.length);
  const resourcePath = 'res://assets/' + relative;
  const input = resolve('.' + source);
  const output = resolve('godot/assets', relative);
  if (nativePortraitFiles.has(output)) throw new Error('Icon import cannot overwrite native portrait ' + resourcePath);
  if (!input.startsWith(resolve('.' + prefix) + '/') || !output.startsWith(resolve('godot/assets') + '/')) {
    throw new Error('Escaping HUD icon path ' + source);
  }
  mkdirSync(dirname(output), { recursive: true });
  if (!existsSync(output)) copyFileSync(input, output);
  else if (!readFileSync(input).equals(readFileSync(output))) throw new Error('Existing HUD icon differs from source: ' + output);
  return { source, resourcePath, sha256: createHash('sha256').update(readFileSync(input)).digest('hex') };
}

const icons: Record<string, ReturnType<typeof image>> = {};
for (const [kind, bindings] of [['weapon', CANDIDATE_01_WEAPON_ART], ['ability', CANDIDATE_01_ABILITY_ART], ['status', CANDIDATE_01_STATUS_ART]] as const) {
  for (const [id, topic] of Object.entries(bindings)) {
    if (typeof topic !== 'string') throw new Error('Missing HUD icon topic ' + kind + ':' + id);
    const source = candidate01AssetUrl(topic);
    const markup = CANDIDATE_01_ART.resolve(provider => kind === 'weapon' ? provider.weaponIcon?.(id)
      : kind === 'ability' ? provider.abilityIcon?.(id) : provider.statusIcon?.(id));
    if (!markup?.includes(source)) throw new Error('Icon mismatch ' + id);
    icons[kind + ':' + id] = image(source);
  }
}
// Portrait declarations and bindings are native-authored; only icons are imported.
writeFileSync(metadataPath, JSON.stringify({ ...metadata, icons, sourceIconKeys: Object.keys(icons) }, null, 2) + '\n');
console.log(`Validated ${Object.keys(metadata.portraits).length} native portrait bindings; exported ${Object.keys(icons).length} source HUD icons.`);
