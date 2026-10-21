import { readFileSync, writeFileSync } from 'node:fs';
import ts from 'typescript';
import { JSDOM } from 'jsdom';
import {
  ContentPackInstaller, createBattleEngine, createContentCatalog, normaliseLevel,
  player, scenarioSignalsOf, strikeCount,
  type ContentPack, type GameEvent, type GameState, type LevelData,
} from '../../../packages/battle-engine/src/index';
import { DefaultBattleEventPresenters, type BattleLogContext } from '../../../packages/game-ui/src/ui/event-presentation';
import { BattleLog } from '../../../packages/game-ui/src/ui/battle-log';
import { resourceLabel } from '../../../packages/game-ui/src/ui/rule-labels';
import { escapeHtml } from '../../../packages/game-ui/src/ui/html';

const destination = process.argv[2];
if (!destination) throw new Error('Usage: oracle.ts <oracle.json>');
const packs: ContentPack[] = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const content = createContentCatalog();
new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const level: LevelData = normaliseLevel(JSON.parse(readFileSync('godot/tests/oracle/source-v1/grayflag-level.json', 'utf8')));
const initial = engine.createState(level);

// Evaluate the actual private projection methods without booting a browser controller.
// This is test-only source execution, not a runtime bridge or a hand-copied oracle.
function sourceMethods(file: string, className: string, names: string[]): object {
  const source = ts.createSourceFile(file, readFileSync(file, 'utf8'), ts.ScriptTarget.Latest, true);
  const declaration = source.statements.find(node => ts.isClassDeclaration(node) && node.name?.text === className);
  if (!declaration || !ts.isClassDeclaration(declaration)) throw new Error(`Missing source class ${className}`);
  const methods = names.map(name => {
    const method = declaration.members.find(node => ts.isMethodDeclaration(node) && node.name.getText(source) === name);
    if (!method) throw new Error(`Missing source method ${className}.${name}`);
    return method.getText(source);
  });
  const javascript = ts.transpileModule(`class SourceProjection { ${methods.join('\n')} }`, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None },
  }).outputText;
  return new Function('player', 'scenarioSignalsOf', 'strikeCount', 'escapeHtml', 'resourceLabel',
    `${javascript}\nreturn SourceProjection.prototype;`)(player, scenarioSignalsOf, strikeCount, escapeHtml, resourceLabel) as object;
}
const gameProjection = sourceMethods('packages/game-ui/src/ui/game.ts', 'GameController', ['logContext']) as {
  logContext(this: unknown): BattleLogContext;
};
interface SourceSummary {
  title: string; chapter: number; outcome: string; turns: number;
  alliesRemaining: number; enemiesRemaining: number; fallen: string[]; signals: string[]; events: GameEvent[];
}
const campaignProjection = sourceMethods('packages/game-ui/src/ui/campaign-game.ts', 'StoryCampaignController', ['summarizeBattle', 'renderBattleResult']) as {
  summarizeBattle(this: unknown, request: { levelId: string; perspectivePlayer: number }, state: GameState, events: GameEvent[]): SourceSummary;
  renderBattleResult(this: unknown, summary: SourceSummary): void;
};
function context(state: GameState): BattleLogContext {
  return gameProjection.logContext.call({ state, session: { content, rules: engine.rules } });
}
const unitNames = Object.fromEntries(content.units.keys().map(id => [id, content.units.get(id).name]));
const careerNames = Object.fromEntries(content.careers.keys().map(id => [id, content.careers.get(id).name]));
const resourceNames = Object.fromEntries(engine.rules.resources.adapters.keys().map(id => [id, resourceLabel(engine.rules.resources, id)]));
const formatCases: unknown[] = [];
function format(name: string, value: object, state: GameState = initial): void {
  const event = value as GameEvent;
  formatCases.push({ name, state, event, expected: DefaultBattleEventPresenters.describe(context(state), event) });
}
const attacker = initial.units[0].id, defender = initial.units.at(-1)!.id;
for (const type of ['attack', 'counter', 'supportAttack', 'partingShot', 'areaAttack']) {
  for (const killed of [false, true]) for (const damage of [0, 30, 0.25, 1e-7, 1e21]) {
    format(`${type} ${killed} ${damage}`, { type, attacker, defender, damage, killed, weapon: 'unused', at: { x: 2, y: 3 } });
  }
}
for (const type of ['attackStructure', 'areaAttackStructure']) for (const destroyed of [false, true])
  format(`${type} ${destroyed}`, { type, attacker, structure: 'test', damage: 12, destroyed });
format('death is silent', { type: 'death', unit: defender });
format('heal', { type: 'heal', source: attacker, target: defender, amount: 17 });
format('missing healer', { type: 'heal', source: 999999, target: defender, amount: 17 });
format('recruit', { type: 'recruit', unit: attacker });
format('recruit current player not owner', { type: 'recruit', unit: attacker }, { ...initial, currentPlayer: initial.players.at(-1)!.id });
format('recruit missing current player and unit', { type: 'recruit', unit: 999999 }, { ...initial, currentPlayer: 999999 });
for (const captured of [false, true]) format(`capture ${captured}`, { type: 'capture', player: initial.currentPlayer, captured, at: { x: 4, y: 5 } });
format('missing capture player', { type: 'capture', player: 999999, captured: true, at: { x: 0, y: 0 } });
format('turn start', { type: 'turnStart', player: initial.currentPlayer, turn: 13 });
for (const resource of [...Object.keys(resourceNames), 'plugin.custom']) {
  for (const subject of [
    { kind: 'player', id: initial.currentPlayer }, { kind: 'player', id: String(initial.currentPlayer) },
    { kind: 'unit', id: attacker }, { kind: 'weapon', id: attacker, slot: 'test' },
    { kind: 'unit', id: 999999 }, { kind: 'plugin.holder', id: '祭坛' }, { kind: 'plugin.holder', id: 3 },
  ]) for (const amount of [-7, 0, 3]) {
    format(`resource ${resource} ${JSON.stringify(subject)} ${amount}`, { type: 'resourceChanged', resource, subject, amount, current: 19 });
  }
}
for (const to of [0, 1, 2, 9]) format(`rank ${to}`, { type: 'rankChanged', unit: attacker, from: 0, to });
for (const to of Object.keys(careerNames)) format(`career ${to}`, { type: 'careerChanged', unit: attacker, to });
format('facing', { type: 'facingChanged', unit: attacker });
format('missing unit attack', { type: 'attack', attacker: 999999, defender, damage: 2, killed: true });
format('missing unit area attack', { type: 'areaAttack', attacker, defender: 999999, damage: 2, killed: true });
format('defeat', { type: 'defeat', player: initial.currentPlayer });
format('missing defeat player', { type: 'defeat', player: 999999 });
format('game over literal markup', { type: 'gameOver', reason: '<胜利> & "保留原文"' });
format('game over UTF16', { type: 'gameOver', reason: '胜\ud800利' });
for (const type of ['scenarioSignal', 'move', 'formationChanged', 'plugin.custom'])
  format(`silent ${type}`, { type, signal: 'do-not-render', unit: attacker });
const noSurvivor = structuredClone(initial);
noSurvivor.units = [];
format('postbatch killed attacker remains neutral fallback', { type: 'attack', attacker, defender, damage: 99, killed: true }, noSurvivor);

const logCases: unknown[] = [];
function logCase(name: string, lines: string[], depth?: number, clearAt?: number): void {
  const log = depth === undefined ? new BattleLog() : new BattleLog(depth);
  const checkpoints: string[][] = [];
  for (const [index, line] of lines.entries()) {
    if (index === clearAt) log.clear();
    log.add(line);
    const snapshot = log.recent();
    checkpoints.push([...snapshot]);
    snapshot.push('external mutation must not enter log');
  }
  logCases.push({ name, depth: depth ?? 60, lines, clearAt: clearAt ?? null, checkpoints });
}
logCase('empty and consecutive runs', ['', 'A', 'A', '', 'A', 'B', 'A']);
logCase('bounded collapsed entries', ['A', 'B', ...Array<string>(200).fill('C'), 'D', 'E', 'E'], 3);
logCase('default 60', Array.from({ length: 70 }, (_, index) => `line ${index}`));
logCase('clear restart', ['A', 'A', 'B', 'B', 'C'], 3, 3);
logCase('zero depth', ['A', 'A', '', 'B'], 0);
logCase('negative depth', ['A', 'A', 'B'], -1);

const strikeEvents = [
  ...['attack', 'counter', 'supportAttack', 'partingShot', 'areaAttack', 'attackStructure', 'areaAttackStructure', 'plugin.strike']
    .map(type => ({ type, attacker, damage: 0 })),
  { type: 'shape-only', attacker: null, damage: null }, { type: 'damage-only', damage: 4 },
  { type: 'attacker-only', attacker }, { type: 'attack' }, { type: 'scenarioSignal', signal: 'bridge-a' },
  { type: 'scenarioSignal', signal: 'bridge-a' }, { type: 'scenarioSignal', signal: 'bridge-b' },
] as unknown as GameEvent[];
const summaries: unknown[] = [];
function summary(name: string, state: GameState, perspectivePlayer: number, events: GameEvent[], title = level.name, chapter = 1): void {
  const result = campaignProjection.summarizeBattle.call({ content, adapter: { level: () => ({ name: title }), chapterOf: () => chapter } },
    { levelId: level.id, perspectivePlayer }, state, events);
  let html = '';
  campaignProjection.renderBattleResult.call({ shell: (value: string) => { html = value; } }, result);
  const root = new JSDOM(html).window.document;
  const text = (selector: string) => root.querySelector(selector)!.textContent;
  const metrics = [...root.querySelectorAll('.result-metrics > div')].map(node => ({ label: node.querySelector('span')!.textContent, value: Number(node.querySelector('b')!.textContent) }));
  const { events: _events, ...data } = result;
  summaries.push({ name, state, perspectivePlayer, title, chapter, events, signals: scenarioSignalsOf(events), strikeCount: strikeCount(events), expected: {
    ...data, strikeCount: strikeCount(events), metrics, heading: text('.campaign-eyebrow'), fallenLabel: text('.result-detail > div:first-child small'),
    signalsLabel: text('.result-detail > div:last-child small'), fallenText: text('.result-detail > div:first-child p'),
    signalsText: text('.result-detail > div:last-child p'), continueLabel: text('.campaign-primary'),
  } });
}
summary('no losses or signals', initial, initial.currentPlayer, []);
const mixed = structuredClone(initial);
const ours = mixed.players[0], enemy = mixed.players.at(-1)!;
ours.controller = 'ai';
mixed.players.push({ ...structuredClone(ours), id: 500, name: 'Allied side' });
mixed.units.push({ ...structuredClone(mixed.units[0]), id: 5000, owner: 500 });
mixed.units.push({ ...structuredClone(mixed.units[0]), id: 5001, owner: 999999 });
mixed.markers = [
  { kind: 'corpse', fallenUnit: { owner: ours.id, type: initial.units[0].type } },
  { kind: 'routed', fallenUnit: { owner: ours.id, type: initial.units[0].type } },
  { kind: 'corpse', fallenUnit: { owner: enemy.id, type: initial.units.at(-1)!.type } },
  { kind: 'corpse', fallenUnit: { owner: 500, type: initial.units[0].type } },
  { kind: 'custom' },
] as GameState['markers'];
mixed.endReason = '原始胜因 <&> "保留"';
summary('explicit AI perspective allied team and repeated fallen', mixed, ours.id, strikeEvents, '本关 <标题>', 3);
summary('opponent perspective', mixed, enemy.id, strikeEvents, '另一方', 2);
summary('no surviving units', { ...mixed, units: [] }, ours.id, strikeEvents);

// Real Grey Flag replay: contexts use the final committed state of each order,
// just as GameController.playEvents does, not a synthetic per-event state.
const replay = JSON.parse(readFileSync('godot/tests/oracle/grayflag-0.35.json', 'utf8'));
const recorded = replay.replay ?? replay;
const replayState = engine.createState(recorded.level ?? level, recorded.seed);
const replayEvents: GameEvent[] = [];
let replaySteps = 0;
for (const action of recorded.actions) {
  const emitted = engine.dispatch(replayState, action.action ?? action);
  replayEvents.push(...emitted);
  for (const [index, event] of emitted.entries()) format(`replay ${replaySteps} event ${index}`, event, structuredClone(replayState));
  replaySteps++;
}
summary('real replay final', replayState, replayState.players[0].id, replayEvents);
writeFileSync(destination, JSON.stringify({ unitNames, careerNames, resourceNames, presenterKeys: DefaultBattleEventPresenters.keys(), formatCases, logCases,
  strikes: { events: strikeEvents, expected: strikeCount(strikeEvents) }, summaries, replaySteps }) + '\n');
console.log(`SOURCE ${formatCases.length} event descriptions, ${logCases.length} log sequences, ${summaries.length} result projections; ${replaySteps} replay actions`);
