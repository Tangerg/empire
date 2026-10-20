import ts from 'typescript';
import { gzipSync } from 'node:zlib';
import { GROUND_TAGS } from '../../packages/game-ui/src/art/ports';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync, copyFileSync, constants } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, type GameState } from '../../packages/battle-engine/src/index';
import { BattleRecorder, hashState } from '../../packages/battle-engine/src/replay';
import { createBattleSave } from '../../packages/battle-engine/src/battle-save';
import { createState } from '../../packages/battle-engine/src/state';
import { SplitMixRandom } from '../../packages/battle-engine/src/random';
import { COMMON_CONTENT_PACK } from '../../packages/content-common/src/index';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '../../packages/content-ancient-empires/src/index';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../../packages/story-candidate-01/src/index';
import { candidateMaterial, CANDIDATE_FOREST_FLOOR, CANDIDATE_SETTLEMENT_LIFE } from '../../packages/story-candidate-01/src/presentation/candidate-01-terrain-materials';
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
write('data/grayflag-level.json', level); write('data/content-packs.json', packs);
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
write('data/art-bindings.json', { ...bindings, CANDIDATE_01_ACTOR_ART: actors });
const scene = read('environment-builder-v1/scenes/c01-01.scene.json');
write('data/grayflag-authored-scene.json', scene);
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
for (const unit of level.units) {
  const actor = unit.key ? actors[unit.key] : undefined;
  if (actor && actor.expectedUnitType !== unit.unit) throw new Error('Actor type mismatch');
  const binding = actor?.unitArt ?? bindings.CANDIDATE_01_UNIT_ART[unit.unit]; if (!binding) throw new Error(`Missing binding ${unit.unit}`);
  topics.add(binding.topicId); if (binding.appearanceId) appearances.add(binding.appearanceId);
}
for (const tile of raw.map.tiles) { const binding = bindings.CANDIDATE_01_MAP_STRUCTURE_ART[tile]; if (binding) { topics.add(binding.topicId); if (binding.presentationId) presentations.add(binding.presentationId); } }
for (const prop of [...scene.placements, ...(scene.framePlacements ?? [])]) if (prop.topicId) topics.add(prop.topicId);
for (const structure of level.structures ?? []) { const topic = bindings.CANDIDATE_01_STRUCTURE_ART[structure.type]; if (topic) topics.add(topic); }
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
for (const asset of assets) Object.assign(asset, copy(asset.png));
write('data/selected-assets.json', { schema: 1, logicalTileSize: 32, assets });
const environment = read('environment-builder-v1/manifest-environment-builder-v1.json');
const ids = new Set([...scene.placements, ...(scene.framePlacements ?? [])].map((entry: { id: string }) => entry.id));
for (const atlas of environment.atlases) if ((atlas.cells ?? []).some((cell: { id: string }) => ids.has(cell.id))) Object.assign(atlas, copy(`environment-builder-v1/${atlas.png2x ?? atlas.png}`));
write('data/environment-catalog.json', environment);
const materials = Object.fromEntries(['field-material', 'road-material', 'forest-floor'].map(name => {
  const spec = read(`world-materials-v1/manifest-${name}-v2.json`);
  return [name, { ...spec, ...copy(`world-materials-v1/${spec.png2x ?? spec.png}`) }];
}));
const mask = read('world-materials-v1/manifest-forest-blend-masks-v1.json').atlases[0];
const geometryAtlas = environment.atlases.find((atlas: { id: string }) => atlas.id === mask.id);
write('data/world-materials.json', { schema: 1, materials, groundTagFamilies: GROUND_TAGS,
  scenery: { forestFloor: CANDIDATE_FOREST_FLOOR, settlementLife: CANDIDATE_SETTLEMENT_LIFE },
  terrainTags: Object.fromEntries([...new Set(raw.map.tiles)].map(id => [id, content.terrains.get(id).tags])),
  forestMask: { ...geometryAtlas, ...mask, ...copy(`world-materials-v1/${mask.png2x ?? mask.png}`) },
  terrainBindings: Object.fromEntries([...new Set(raw.map.tiles)].map(id => [id, candidateMaterial(content, id)])) });

write('data/source-provenance.json', { schema: 1, sourceRevision: execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim(), fixtures, selectedAssets: assets.length,
  recovery: 'Reconstructed from verified remote bfe9334a after workspace reset. Local checkpoints cc43ac3a, 904351d9 and 8bbca062 are unavailable. Actor overrides were re-exported from the recovered source declaration.' });
console.log(JSON.stringify({ fixtures, selectedAssets: assets.length }));
