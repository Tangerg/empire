import ts from 'typescript';
import { gzipSync } from 'node:zlib';
import { GROUND_TAGS } from '../../packages/game-ui/src/art/ports';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync, copyFileSync, constants, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, type GameState } from '../../packages/battle-engine/src/index';
import { BattleRecorder, hashState } from '../../packages/battle-engine/src/replay';
import { createBattleSave } from '../../packages/battle-engine/src/battle-save';
import { createState } from '../../packages/battle-engine/src/state';
import { SplitMixRandom } from '../../packages/battle-engine/src/random';
import { COMMON_CONTENT_PACK } from '../../packages/content-common/src/index';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '../../packages/content-ancient-empires/src/index';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS, CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN, candidate01Level } from '../../packages/story-candidate-01/src/index';
import { candidateMaterial, CANDIDATE_FOREST_FLOOR, CANDIDATE_SETTLEMENT_LIFE, CANDIDATE_RIVER_STONES, CANDIDATE_SHORE_DECALS, CANDIDATE_SHORE, CANDIDATE_VERTICAL_CROSSINGS, CANDIDATE_FOUNDATIONS, CANDIDATE_SURFACE_FIT } from '../../packages/story-candidate-01/src/presentation/candidate-01-terrain-materials';
import * as bindings from '../../packages/story-candidate-01/src/presentation/candidate-01-bindings';
const assetRoot = resolve('packages/story-candidate-01/assets/final-fantasy-v1');
const output = resolve('godot');
const read = (path: string) => JSON.parse(readFileSync(resolve(assetRoot, path), 'utf8'));
const hash = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
function write(path: string, value: unknown): void {
  const destination = resolve(output, path); mkdirSync(dirname(destination), { recursive: true });
  writeFileSync(destination, JSON.stringify(value, (_key, entry) => {
    if (typeof entry === 'function' || typeof entry === 'bigint' || entry instanceof Map || entry instanceof Set) throw new Error('Not plain JSON');
    return entry;
  }, 2) + '\n');
}
const packs = [COMMON_CONTENT_PACK, ANCIENT_EMPIRES_CONTENT_PACK, CANDIDATE_01_CONTENT_PACK];
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content }); const level = candidate01Level('c01-01');
const raw = createState(engine.rules, level);
// Native gameplay documents are authored independently; these exports are historical source evidence.
write('tests/oracle/source-v1/grayflag-level.json', level); write('tests/oracle/source-v1/content-packs.json', packs);
const campaign = CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN;
write('tests/oracle/source-v1/grayflag-campaign.json', campaign);
write('tests/oracle/source-v1/grayflag-campaign-levels.json', CANDIDATE_01_LEVELS);
write('tests/oracle/grayflag-initial-raw.json', { level, state: raw });
const actorSource = ts.createSourceFile('actors.ts', readFileSync('packages/story-candidate-01/src/presentation/candidate-01-actor-art.ts', 'utf8'), ts.ScriptTarget.Latest, true);
function literal(node: ts.Node): unknown {
  if (ts.isStringLiteral(node)) return node.text;
  if (ts.isObjectLiteralExpression(node)) return Object.fromEntries(node.properties.map(property => {
    if (!ts.isPropertyAssignment(property) || !(ts.isIdentifier(property.name) || ts.isStringLiteral(property.name))) throw new Error('Nonliteral actor binding');
    return [property.name.text, literal(property.initializer)];
  }));
  throw new Error('Nonliteral actor binding value');
}
let actors: Record<string, { expectedUnitType: string; unitArt: { topicId: string; appearanceId?: string } }> | undefined;
function visit(node: ts.Node): void {
  if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.name.text === 'CANDIDATE_01_ACTOR_ART') {
    if (!node.initializer || !ts.isCallExpression(node.initializer)) throw new Error('Actor declaration changed');
    actors = literal(node.initializer.arguments[1]) as typeof actors;
  }
  ts.forEachChild(node, visit);
}
visit(actorSource); if (!actors) throw new Error('Missing actor declaration');
const presentationSource = ts.createSourceFile('presentation.ts', readFileSync('packages/story-candidate-01/src/presentation/index.ts', 'utf8'), ts.ScriptTarget.Latest, true);
let healFx: string | undefined;
function visitPresentation(node: ts.Node): void {
  if (ts.isPropertyAssignment(node) && ts.isIdentifier(node.name) && node.name.text === 'healFx') {
    if (healFx !== undefined || !ts.isStringLiteral(node.initializer)) throw new Error('Heal FX binding changed');
    healFx = node.initializer.text;
  }
  ts.forEachChild(node, visitPresentation);
}
visitPresentation(presentationSource);
if (!healFx) throw new Error('Missing source heal FX binding');
write('data/art-bindings.json', { ...bindings, CANDIDATE_01_ACTOR_ART: actors, CANDIDATE_01_HEAL_FX: healFx });
const scene = read('environment-builder-v1/scenes/c01-01.scene.json');
write('data/grayflag-authored-scene.json', scene);
const scenes = CANDIDATE_01_LEVELS.map(candidate => {
  const path = `environment-builder-v1/scenes/${candidate.id}.scene.json`;
  if (!existsSync(resolve(assetRoot, path))) return null;
  const authored = read(path); write(`data/scenes/${candidate.id}.json`, authored); return authored;
}).filter(Boolean);
const campaignStates = CANDIDATE_01_LEVELS.map(candidate => createState(engine.rules, candidate));
const terrainIds = [...new Set(campaignStates.flatMap(state => state.map.tiles))];
const fixtures = [];
const actionCases: unknown[] = [];
for (const aggression of [0.35, 0.58, 0.8]) {
  const oracleLevel = structuredClone(level);
  for (const player of oracleLevel.players) { player.controller = 'ai'; player.ai = { aggression }; }
  const state = engine.createState(oracleLevel); const initialState = structuredClone(state);
  const recorder = new BattleRecorder(engine, state); const steps = [];
  while (state.phase !== 'over' && steps.length < 800) {
    const action = engine.chooseAiAction(state); recorder.record(action);
    const before = structuredClone(state);
    const events = engine.dispatch(state, action);
    if (aggression === 0.58) actionCases.push({ action, before, events, after: structuredClone(state) });
    steps.push({ action, events, stateHash: hashState(state) });
  }
  if (state.phase !== 'over') throw new Error('Oracle did not finish');
  const replay = recorder.replay(state); const check = engine.createState(oracleLevel, { seed: replay.seed });
  for (const step of steps) {
    if (JSON.stringify(engine.dispatch(check, step.action)) !== JSON.stringify(step.events) || hashState(check) !== step.stateHash) throw new Error('Oracle replay mismatch');
  }
  write(`tests/oracle/grayflag-${aggression}.json`, { schema: 1, level: oracleLevel, initialState, replay, steps,
    finalSave: createBattleSave(engine.rulesetManifest, state, '2026-10-20T00:00:00.000Z') });
  fixtures.push({ aggression, actions: steps.length, finalHash: hashState(state) });
}
write('tests/oracle/action-cases.json', actionCases);
writeFileSync(resolve(output, 'tests/oracle/action-cases.json.gz'), gzipSync(readFileSync(resolve(output, 'tests/oracle/action-cases.json'))));
const vectors = [];
for (const seed of [0, 1, 2147483648, 4294967295]) {
  const state = engine.createState(level, { seed });
  for (const stream of ['combat', 'ai', '灰旗', '😀']) vectors.push({ seed, stream, draws: Array.from({ length: 8 }, () => SplitMixRandom.next(state, stream)) });
}
write('tests/oracle/random-vectors.json', vectors);
const values = [
  { text: '灰旗😀\u0000\n\t"\\', lone: '\ud800', numbers: [0, -0, 1e-7, 1e-6, 1e20, 1e21, 1.7976931348623157e308] },
  { '😀': 1, '\ue000': 2, levelName: 'ignored', players: [{ controller: 'human', id: 1 }], nested: { levelName: 'included' } },
];
write('tests/oracle/digest-vectors.json', values.map(value => ({ value, hash: hashState(value as unknown as GameState) })));
const geometry = [];
for (const id of ['square4', 'square8', 'hex']) {
  const grid = engine.rules.grids.get(id);
  for (const at of [{ x: 0, y: 0 }, { x: 3, y: 2 }, { x: 3, y: 3 }, { x: -2, y: -1 }]) {
    for (const to of [{ x: 0, y: 0 }, { x: 4, y: 4 }, { x: -3, y: 2 }]) geometry.push({ id, at, to, distance: grid.distance(at, to), toward: grid.toward(at, to), adjacent: grid.adjacent(at), within: grid.within(at, 1, 2), line: grid.line(at, to), center: grid.center(at) });
  }
}
write('tests/oracle/geometry-vectors.json', geometry);
const manifest = read('manifest-final-fantasy-v1.json'); const hd = read('manifest-tactical-runtime-hd.json');
const topics = new Set<string>(); const appearances = new Set<string>(); const presentations = new Set<string>();
for (const unit of CANDIDATE_01_LEVELS.flatMap(candidate => candidate.units)) {
  const actor = unit.key ? actors[unit.key] : undefined;
  if (actor && actor.expectedUnitType !== unit.unit) throw new Error('Actor type mismatch');
  const binding = actor?.unitArt ?? bindings.CANDIDATE_01_UNIT_ART[unit.unit]; if (!binding) throw new Error(`Missing binding ${unit.unit}`);
  topics.add(binding.topicId); if (binding.appearanceId) appearances.add(binding.appearanceId);
}
for (const tile of terrainIds) { const binding = bindings.CANDIDATE_01_MAP_STRUCTURE_ART[tile]; if (binding) { topics.add(binding.topicId); if (binding.presentationId) presentations.add(binding.presentationId); } }
for (const prop of scenes.flatMap(scene => [...scene.placements, ...(scene.framePlacements ?? [])])) if (prop.topicId) topics.add(prop.topicId);
for (const structure of CANDIDATE_01_LEVELS.flatMap(candidate => candidate.structures ?? [])) { const topic = bindings.CANDIDATE_01_STRUCTURE_ART[structure.type]; if (topic) topics.add(topic); }
for (const topic of [...Object.values(bindings.CANDIDATE_01_STRUCTURE_ART), 'C01-BPROP-COVER-1', 'C01-BPROP-COVER-2', 'C01-BPROP-AFTERMATH-2', 'C01-BPROP-AFTERMATH-3', 'C01-BPROP-EVIDENCE-1']) if (topic !== undefined) topics.add(topic);
for (const topic of [...Object.values(bindings.CANDIDATE_01_WEAPON_FX), healFx]) if (topic !== undefined) topics.add(topic);
const assets = manifest.assets.filter((record: { topicId: string }) => topics.has(record.topicId)).map((record: { topicId: string; png: string; width: number; height: number }) => {
  const physical = hd.assets.find((entry: { topicId: string }) => entry.topicId === record.topicId);
  return { ...record, png: physical?.png ?? record.png, physicalWidth: physical?.width ?? record.width, physicalHeight: physical?.height ?? record.height, pixelDensity: physical?.pixelDensity ?? 1 };
});
for (const record of read('manifest-hero-appearances.json').appearances) if (appearances.has(record.id)) assets.push({ ...record, png: record.hdPng, physicalWidth: record.hdWidth, physicalHeight: record.hdHeight });
for (const record of read('manifest-map-structure-presentations.json').variants) if (presentations.has(record.id)) assets.push({ ...record, png: record.hdPng, physicalWidth: record.hdWidth, physicalHeight: record.hdHeight });
function copy(png: string): { resourcePath: string; sha256: string } {
  const source = resolve(assetRoot, png); if (!source.startsWith(assetRoot + '/')) throw new Error('Escaping asset path');
  const destination = resolve(output, 'assets', png); mkdirSync(dirname(destination), { recursive: true });
  copyFileSync(source, destination, constants.COPYFILE_FICLONE); return { resourcePath: `res://assets/${png}`, sha256: hash(readFileSync(source)) };
}
// Native narrative documents and PO own structure and wording. This importer only
// refreshes the exact assets they reference; it never regenerates those documents.
const narrativeMetadata = JSON.parse(readFileSync(resolve(output, 'data/grayflag-staging.json'), 'utf8'));
function copyNarrativeAssets(value: unknown): void {
  if (!value || typeof value !== 'object') return;
  if ('source' in value && 'resourcePath' in value && 'sha256' in value) {
    const art = value as { source: string; resourcePath: string; sha256: string };
    if (art.source.startsWith('/godot/assets/')) {
      const relative = art.source.slice('/godot/'.length);
      const nativeSource = resolve(output, relative);
      if (!nativeSource.startsWith(resolve(output, 'assets') + '/') || art.resourcePath !== `res://${relative}`
        || hash(readFileSync(nativeSource)) !== art.sha256)
        throw new Error(`Native-owned narrative asset contract mismatch: ${art.source}`);
      return;
    }
    const prefix = '/packages/story-candidate-01/assets/final-fantasy-v1/';
    if (!art.source.startsWith(prefix)) throw new Error(`Unknown narrative asset root: ${art.source}`);
    const relative = art.source.slice(prefix.length);
    const source = resolve(assetRoot, relative);
    if (!source.startsWith(assetRoot + '/') || art.resourcePath !== `res://assets/${relative}` || hash(readFileSync(source)) !== art.sha256)
      throw new Error(`Native narrative asset contract mismatch: ${art.source}`);
    copy(relative);
  }
  for (const child of Object.values(value)) copyNarrativeAssets(child);
}
copyNarrativeAssets(narrativeMetadata);
for (const asset of assets) Object.assign(asset, copy(asset.png));
write('data/selected-assets.json', { schema: 1, logicalTileSize: 32, assets });
const environment = read('environment-builder-v1/manifest-environment-builder-v1.json');
const ids = new Set(scenes.flatMap(scene => [...scene.placements, ...(scene.framePlacements ?? [])]).map((entry: { id: string }) => entry.id));
const terrainBindings = Object.fromEntries(terrainIds.map(id => [id, candidateMaterial(content, id)]));
const bindingText = JSON.stringify([terrainBindings, CANDIDATE_RIVER_STONES, CANDIDATE_SHORE_DECALS, CANDIDATE_FOUNDATIONS, CANDIDATE_VERTICAL_CROSSINGS]);
for (const atlas of environment.atlases) if (bindingText.includes(JSON.stringify(atlas.id)) || (atlas.cells ?? []).some((cell: { id: string }) => ids.has(cell.id) || bindingText.includes(JSON.stringify(cell.id)))) Object.assign(atlas, copy(`environment-builder-v1/${atlas.png2x ?? atlas.png}`));
write('data/environment-catalog.json', environment);
const edgeDecorations = read('edge-decoration-v1/manifest-edge-decoration-v1.json');
for (const asset of edgeDecorations.assets) Object.assign(asset, copy(`edge-decoration-v1/${asset.png2x ?? asset.png}`));
write('data/edge-decorations.json', edgeDecorations);
const materials = Object.fromEntries(['field-material', 'road-material', 'forest-floor', 'water-material', 'earth-floor', 'stone-base', 'stone-paving', 'forge-base', 'graveyard-base', 'meadow-stony', 'wasteland-scorched', 'wasteland-scree'].map(name => {
  const spec = read(`world-materials-v1/manifest-${name}-v2.json`);
  return [name, { ...spec, ...copy(`world-materials-v1/${spec.png2x ?? spec.png}`), ...(spec.occupancy ? { occupancy: { ...spec.occupancy, ...copy(`world-materials-v1/${spec.occupancy.png2x ?? spec.occupancy.png}`) } } : {}) }];
}));
const mask = read('world-materials-v1/manifest-forest-blend-masks-v1.json').atlases[0];
const geometryAtlas = environment.atlases.find((atlas: { id: string }) => atlas.id === mask.id);
function maskRecords(file: string) { return read(`world-materials-v1/${file}`).atlases.map((mask: { id: string; png2x?: string; png: string }) => ({ ...environment.atlases.find((atlas: { id: string }) => atlas.id === mask.id), ...mask, ...copy(`world-materials-v1/${mask.png2x ?? mask.png}`) })); }
function crossing(file: string) { const spec = read(`registered-crossings-v1/${file}`); return { ...spec, ...(spec.png ? copy(`registered-crossings-v1/${spec.png2x ?? spec.png}`) : {}) }; }
write('data/world-materials.json', { edgeDecorationsResourcePath: 'res://data/edge-decorations.json', foundations: CANDIDATE_FOUNDATIONS, surfaceFit: CANDIDATE_SURFACE_FIT, shoreBlend: CANDIDATE_SHORE,
  earthMask: maskRecords('manifest-earth-blend-masks-v1.json')[0],
  connectorMasks: Object.fromEntries(maskRecords('manifest-stone-connectors-v1.json').map((value: { id: string }) => [value.id, value])),
  crossings: { horizontal: crossing('manifest-crossing-v1.json'), vertical: crossing('manifest-crossing-vertical-v1.json'), junction: crossing('manifest-junction-v1.json'), verticalLegacyIds: CANDIDATE_VERTICAL_CROSSINGS }, schema: 1, materials, groundTagFamilies: GROUND_TAGS,
  scenery: { forestFloor: CANDIDATE_FOREST_FLOOR, settlementLife: CANDIDATE_SETTLEMENT_LIFE, riverStones: CANDIDATE_RIVER_STONES, shoreDecals: CANDIDATE_SHORE_DECALS },
  terrainTags: Object.fromEntries(terrainIds.map(id => [id, content.terrains.get(id).tags])),
  forestMask: { ...geometryAtlas, ...mask, ...copy(`world-materials-v1/${mask.png2x ?? mask.png}`) },
  terrainBindings: Object.fromEntries(terrainIds.map(id => [id, candidateMaterial(content, id)])) });

write('data/source-provenance.json', { schema: 1, sourceRevision: 'bfe9334a29630351ba4f46fd4c195cb3a72c36d9', fixtures, selectedAssets: assets.length,
  recovery: 'Reconstructed from verified remote bfe9334a after workspace reset. Local checkpoints cc43ac3a, 904351d9 and 8bbca062 are unavailable. Actor overrides were re-exported from the recovered source declaration.' });
console.log(JSON.stringify({ fixtures, selectedAssets: assets.length }));
