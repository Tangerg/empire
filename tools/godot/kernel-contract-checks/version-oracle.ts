import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createContentCatalog, createBattleEngine, type ContentPack, type LevelData } from '../../../packages/battle-engine/src/index';
import { SrpgMicrokernel } from '../../../packages/battle-engine/src/kernel';
import { createBattleRulesetManifest, requirePersistentRuleset, rulesetDifferences } from '../../../packages/battle-engine/src/ruleset-manifest';
import { BattleSaveReader, createBattleSave } from '../../../packages/battle-engine/src/battle-save';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { DefaultCampaignNodes, type CampaignNodeInspection } from '../../../packages/campaign-engine/src/nodes';
import type { CampaignDefinition } from '../../../packages/campaign-engine/src/types';

const packs: ContentPack[] = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const content = createContentCatalog();
new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const level: LevelData = JSON.parse(readFileSync('godot/tests/oracle/source-v1/grayflag-level.json', 'utf8'));
const state = engine.createState(level);
const features = {
  abilities: engine.rules.abilities.keys(), reactions: engine.rules.reactions.keys(), directives: engine.rules.directives.keys(),
  turnOrders: engine.rules.turnOrders.keys(), grids: engine.rules.grids.keys(), objectives: engine.rules.objectives.keys(),
  scenarioConditions: engine.rules.scenarioConditions.keys(), scenarioEffects: engine.rules.scenarioEffects.keys(),
  areaShapes: engine.rules.areaShapes.keys(), hitEffects: engine.rules.hitEffects.keys(), castAbilities: [],
  resources: engine.rules.resources.adapters.keys(), tacticEffects: [...new Set(content.tactics.all().flatMap(tactic => tactic.effects.map(effect => effect.type)))],
  stateFeatures: ['composites', 'deployment', 'embarkedUnits'],
};
const reader = new BattleSaveReader();
const savedAt = '2026-10-20T00:00:00.000Z';
const versions = [2147483648, 9007199254740992, 1e20, 1e21, Number.MAX_VALUE];
const cases = versions.map(version => {
  const kernel = new SrpgMicrokernel().use({ id: 'test.versioned', version, install() {} });
  const manifest = createBattleRulesetManifest({ content, plugins: kernel.compose().pluginManifest });
  requirePersistentRuleset(manifest);
  const save = reader.load(createBattleSave(manifest, state, savedAt), { rules: engine.rules, rulesetManifest: manifest });
  const loaded = reader.load(save, { rules: engine.rules, rulesetManifest: manifest });
  const changed = createBattleRulesetManifest({ content, plugins: new Map([['test.versioned', version / 2]]) });
  return { version, manifest, savedRuleset: save.ruleset, loadedStateHash: hashState(loaded.state), differences: rulesetDifferences(manifest, changed) };
});
const manifest = cases[0].manifest;
const errors = ['NaN', 'Infinity', '-Infinity', 1.5, 0, -1].map(encoded => {
  const corrupt = createBattleSave(manifest, state, savedAt);
  (corrupt.ruleset.plugins as Record<string, number>)['test.versioned'] = Number(encoded);
  let error = '';
  try { reader.load(corrupt, { rules: engine.rules, rulesetManifest: manifest }); } catch (caught) { error = (caught as Error).message; }
  return { version: encoded, error };
});
const whitespace = ['\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u0020', '\u00A0', '\u1680',
  '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A',
  '\u2028', '\u2029', '\u202F', '\u205F', '\u3000', '\uFEFF'];
const idCases = ['', ...whitespace, whitespace.join(''), '\u0085', '\u180E', '\u200B', '\uD800', '\uDFFF', ' \uD800 ', ' named '].map(id => {
  const rawManifest = createBattleRulesetManifest({ content, plugins: new Map([[id, 1]]) });
  const document = createBattleSave(rawManifest, state, savedAt);
  let error: string | null = null;
  let loadedStateHash: string | null = null;
  try { loadedStateHash = hashState(reader.load(document, { rules: engine.rules, rulesetManifest: rawManifest }).state); }
  catch (caught) { error = (caught as Error).message; }
  let registrationError: string | null = null;
  try { new SrpgMicrokernel().use({ id, version: 1, install() {} }); }
  catch (caught) { registrationError = (caught as Error).message; }
  return { id, blank: id.trim().length === 0, manifest: rawManifest, error, loadedStateHash, registrationError };
});
const boundaryCases = ['\uFEFF', '\u0085'].map(id => {
  let packError: string | null = null;
  try { new ContentPackInstaller(createContentCatalog()).install({ id, version: 1 }); }
  catch (caught) { packError = (caught as Error).message; }
  const definition: CampaignDefinition = { schema: 1, id: 'blank-boundary', version: 1, start: 'battle', contentPacks: {}, nodes: [{ id: 'battle', type: 'battle', level: id, next: {} }] };
  const inspection: CampaignNodeInspection = {
    definition, hasNode: key => definition.nodes.some(node => node.id === key), hasRosterUnit: () => false,
    reject: message => { throw new Error(message); },
  };
  let battleError: string | null = null;
  try { DefaultCampaignNodes.validate(inspection, definition.nodes[0]); }
  catch (caught) { battleError = (caught as Error).message; }
  return { id, definition, packError, battleError };
});
writeFileSync(process.argv[2], JSON.stringify({ state, features, savedAt, cases, errors, idCases, boundaryCases, defaultManifest: engine.rulesetManifest }, null, 2) + '\n');
