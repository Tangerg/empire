import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import ts from 'typescript';
import { JSDOM } from 'jsdom';
import { CampaignRuntime, createCampaignSave, type CampaignDefinition, type CampaignState } from '../../../packages/campaign-engine/src/index';
import { loadCampaignState, saveCampaignState, deleteCampaignState } from '../../../packages/game-ui/src/application/campaign-storage';
import { CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN as definition } from '../../../packages/story-candidate-01/src/index';
import { CANDIDATE_01_MENU_ART } from '../../../packages/story-candidate-01/src/presentation/index';
import { icon } from '../../../packages/game-ui/src/art/icons';
import { escapeHtml } from '../../../packages/game-ui/src/ui/html';

const output = resolve(process.argv[2] ?? '');
if (!output.startsWith('/tmp/empire-campaign-slot.') || existsSync(output))
  throw new Error('Oracle output must be a new file inside a new /tmp/empire-campaign-slot.* directory.');
const key = `empire:campaign:${definition.id}:v${definition.version}`;
let raw: string | null = null;
let readError = false, writeError = false;
let writes = 0, deletes = 0;
const local = {
  getItem(candidate: string): string | null { if (readError) throw new Error('storage read unavailable'); return candidate === key ? raw : null; },
  setItem(candidate: string, value: string): void { if (writeError) throw new Error('storage quota exceeded'); if (candidate !== key) throw new Error('Unexpected storage key'); writes++; raw = value; },
  removeItem(candidate: string): void { if (candidate !== key) throw new Error('Unexpected deletion'); deletes++; raw = null; },
};
Object.defineProperty(globalThis, 'localStorage', { value: local, configurable: true });
const initial = new CampaignRuntime(definition).snapshot();
const stamp = '2026-10-20T00:00:00.000Z';
const save = (state: CampaignState = initial) => createCampaignSave(definition, state, stamp);
function withProgress(status: CampaignState['status'], count: number): CampaignState {
  const battles = definition.nodes.filter(node => node.type === 'battle').slice(0, count);
  return { ...structuredClone(initial), status, battleSequence: count, battleHistory: battles.map((node, index) => ({
    requestId: `fixture-${index}`, node: node.id, level: node.level, outcome: 'victory', turns: index + 1, signals: [],
  })) };
}
const cases: unknown[] = [];
function record(name: string, value: string | null, options: { definition?: CampaignDefinition; readError?: boolean; syntax?: boolean } = {}): void {
  raw = value; readError = options.readError ?? false;
  let expected: unknown;
  try { expected = { kind: 'returned', ...loadCampaignState(options.definition ?? definition) }; }
  catch (error) { expected = { kind: 'thrown', name: (error as Error).name, message: (error as Error).message }; }
  if (raw !== value) throw new Error('Source load modified the slot');
  cases.push({ name, raw: value, definition: options.definition ?? definition, readError, syntax: options.syntax ?? false, expected });
  readError = false;
}
record('absent', null); record('empty string', '');
for (const value of ['{broken json', ' ', '{"schema":', 'NaN']) record(`malformed JSON ${value}`, value, { syntax: true });
for (const value of [null, [], 1, true, 'text', {}, { schema: 0 }, { ...save(), schema: 2 },
  { ...save(), state: null }, { ...save(), contentPacks: {} }, { ...save(), savedAt: 'not-a-date' },
  { ...save(), state: { ...initial, status: 'unknown' } }, { ...save(), state: { ...initial, currentNode: 'missing' } },
  { ...save(), state: { ...initial, flags: [5] } }]) record(`envelope ${cases.length}`, JSON.stringify(value));
for (const status of ['active', 'completed', 'failed'] as const) record(status, JSON.stringify(save({ ...initial, status })));
const firstBattle = definition.nodes.find(node => node.type === 'battle')!;
if (firstBattle.type !== 'battle') throw new Error('Missing first source battle');
const pending: CampaignState = { ...initial, currentNode: firstBattle.id, battleSequence: 1,
  pendingBattle: { requestId: 'source-pending', node: firstBattle.id, level: firstBattle.level } };
record('pending battle', JSON.stringify(save(pending)));
record('progressed campaign', JSON.stringify(save(withProgress('active', 7))));
record('definition defect', JSON.stringify(save()), { definition: { ...definition, start: 'missing' } });
record('read error', JSON.stringify(save()), { readError: true });
const writeCases: unknown[] = [];
for (const [name, state, fails] of [['clean', initial, false], ['pending', pending, false], ['write error', initial, true]] as const) {
  raw = 'original'; writes = 0; writeError = fails;
  let expected: unknown;
  try { saveCampaignState(definition, state); expected = { kind: 'returned', save: JSON.parse(raw) }; }
  catch (error) { expected = { kind: 'thrown', name: (error as Error).name, message: (error as Error).message }; }
  writeCases.push({ name, state, expected, writes, retained: raw === 'original' });
}
writeError = false;

// Execute the actual original title projection and click handler. Non-campaign destinations
// are unused stubs, not a reimplementation of campaign menu decisions.
const path = 'apps/game/src/main.ts';
const source = ts.createSourceFile(path, readFileSync(path, 'utf8'), ts.ScriptTarget.Latest, true);
function declaration(name: string): string {
  const found = source.statements.find(node =>
    (ts.isFunctionDeclaration(node) && node.name?.text === name) ||
    (ts.isVariableStatement(node) && node.declarationList.declarations.some(item => item.name.getText(source) === name)));
  if (!found) throw new Error(`Missing source declaration: ${name}`);
  return found.getText(source);
}
const javascript = ts.transpileModule(['titleItem', 'skirmishMarkup', 'renderMenu'].map(declaration).join('\n'),
  { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None } }).outputText;
const menuCases: unknown[] = [];
let menuText: unknown;
let newLabel: string | null = null;
for (const [name, state, rejection] of [
  ['no-save', null, null], ['active', initial, null],
  ['active-progress', withProgress('active', 7), null],
  ['completed', withProgress('completed', 16), null], ['failed', withProgress('failed', 2), null],
  ['invalid-save', null, 'corrupt <&> progress'],
] as const) {
  const dom = new JSDOM('<div id="app"></div>');
  const app = dom.window.document.getElementById('app')!;
  raw = state ? JSON.stringify(save(state)) : rejection ? '{broken' : null;
  const loaded = rejection ? { state: null, rejected: rejection } : loadCampaignState(definition);
  let approved = false;
  const confirmations: string[] = [];
  const opens: unknown[] = [];
  const render = new Function('document', 'app', 'readCustomLevels', 'loadCampaignState', 'CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN',
    'CANDIDATE_01_MENU_ART', 'BUILTIN_LEVELS', 'escapeHtml', 'icon', 'confirm', 'openCampaign', 'deleteCampaignState',
    `let active = null; ${javascript}; return renderMenu;`)(dom.window.document, app,
      () => ({ levels: [], rejected: [] }), () => loaded, definition, CANDIDATE_01_MENU_ART, [], escapeHtml, icon,
      (text: string) => { confirmations.push(text); return approved; }, (value: unknown) => opens.push(value), deleteCampaignState) as () => void;
  render();
  const text = (selector: string) => app.querySelector(selector)?.textContent?.trim() ?? null;
  const expected = { title: text('h1'), kicker: text('.title-kicker'), description: text('.title-sub'),
    artSource: app.querySelector('img')!.getAttribute('src'), artAlt: app.querySelector('img')!.getAttribute('alt'),
    canContinue: app.querySelector('[data-act="campaignContinue"]') !== null,
    continueLabel: text('[data-act="campaignContinue"] > span'), progress: text('[data-act="campaignContinue"] > small'),
    newLabel: text('[data-act="campaignNew"] > span'), newPrimary: app.querySelector('[data-act="campaignNew"]')!.classList.contains('primary'),
    notice: null as string | null };
  if (rejection) {
    (app.querySelector('[data-act="skirmish"]') as HTMLElement).click();
    expected.notice = text('.empty-note');
  }
  deletes = 0;
  (app.querySelector('[data-act="campaignNew"]') as HTMLElement).click();
  const declined = { confirmations: confirmations.length, deletes, opens: opens.length };
  approved = true;
  (app.querySelector('[data-act="campaignNew"]') as HTMLElement).click();
  const accepted = { confirmations: confirmations.length, deletes, opens: opens.length };
  if (state) (app.querySelector('[data-act="campaignContinue"]') as HTMLElement).click();
  const prompt = confirmations[0] ?? null;
  menuCases.push({ name, state, rejected: rejection, expected, prompt, declined, accepted,
    continuedOriginalState: state ? opens.at(-1) === loaded.state : null });
  if (name === 'no-save') newLabel = expected.newLabel;
  if (state && name === 'active') menuText = { title: expected.title, kicker: expected.kicker, description: expected.description,
    artAlt: expected.artAlt, continueLabel: expected.continueLabel, newLabel, restartLabel: expected.newLabel, replacePrompt: prompt };
}
writeFileSync(output, JSON.stringify({ definition, initial, pending, cases, writeCases, menuCases,
  metadata: { schema: 1, title: '断冠之誓', battleTotal: definition.nodes.filter(node => node.type === 'battle').length,
    menuArt: { source: CANDIDATE_01_MENU_ART, resourcePath: null }, menu: menuText } }) + '\n', { flag: 'wx' });
console.log(`SOURCE ${cases.length} storage cases, ${writeCases.length} writes and ${menuCases.length} real title-menu renders/confirmation flows.`);
