import { readFileSync, writeFileSync } from 'node:fs';
import {
  ContentPackInstaller, createBattleEngine, createContentCatalog,
  type Action, type ContentPack, type Coord, type GameState, type LevelData,
} from '../../../packages/battle-engine/src/index';
import { createState } from '../../../packages/battle-engine/src/state';
import { deploymentRefusal, deploymentRoster, deploymentSpots } from '../../../packages/battle-engine/src/deployment';
import { hashState } from '../../../packages/battle-engine/src/replay';

const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8')) as ContentPack[];
const content = createContentCatalog();
new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const at = (x: number, y = 0): Coord => ({ x, y });
const make = (terrain = ['......', '......']): LevelData => ({
  schema: 2, id: 'deployment-fixture', name: 'Deployment fixture', width: 6, height: 2, terrain,
  owners: [], rules: {}, victory: [{ type: 'routEnemies' }],
  players: [
    { id: 1, name: 'Blue', team: 1, color: '#00f', controller: 'human', resources: {} },
    { id: 2, name: 'Red', team: 2, color: '#f00', controller: 'human', resources: {} },
    { id: 3, name: 'Blue ally', team: 1, color: '#0ff', controller: 'human', resources: {} },
  ],
  units: [
    { key: 'foot', unit: 'soldier', owner: 1, ...at(0) },
    { key: 'horse', unit: 'knight', owner: 1, ...at(1) },
    { key: 'red', unit: 'soldier', owner: 2, ...at(5, 1) },
    { key: 'ally', unit: 'soldier', owner: 3, ...at(3, 1) },
  ],
  scenario: { zones: [
    { id: 'blue', cells: [at(0), at(1), at(2), at(3), at(2, 1), at(3, 1), at(5, 1)] },
    { id: 'red', cells: [at(5, 1), at(4, 1), at(5)] },
  ] },
  deployment: { order: [1, 2], zones: [{ player: 1, zone: 'blue' }, { player: 2, zone: 'red' }] },
});
type Fixture = { name: string; level: LevelData; edit?: (state: GameState) => void };
const fixtures: Fixture[] = [{ name: 'two-sides-and-ally-blocker', level: make() }];
const split = make(['..^...', '......']);
split.scenario!.zones!.push({ id: 'ridge', cells: [at(2)] });
split.deployment!.zones = [
  { player: 1, zone: 'blue', unitKeys: ['horse'] },
  { player: 1, zone: 'ridge', unitKeys: ['foot'] },
  { player: 2, zone: 'red' },
];
fixtures.push({ name: 'split-assignment-and-reverse-swap-zone', level: split });
fixtures.push({ name: 'reverse-swap-terrain', level: make(['^.....', '......']) });
fixtures.push({ name: 'impassable-water', level: make(['..~...', '......']) });
const reversed = make();
reversed.deployment!.order = [2, 1, 2];
reversed.deployment!.zones[0].unitKeys = ['horse', 'foot'];
fixtures.push({ name: 'order-dedup-and-level-unit-order', level: reversed });
const overlapping = structuredClone(split);
overlapping.scenario!.zones![2].cells = [at(2), at(0)];
fixtures.push({ name: 'overlapping-zone-order', level: overlapping });
const empty = make();
empty.deployment!.zones[0].unitKeys = [];
fixtures.push({ name: 'empty-roster-can-confirm', level: empty });
const inferred = make();
delete inferred.deployment!.order;
fixtures.push({ name: 'inferred-player-order', level: inferred });
for (const grid of ['square8', 'hex']) {
  const level = make(); level.rules.grid = grid;
  fixtures.push({ name: `grid-${grid}`, level });
}
const initiative = make(); initiative.rules.turnOrder = 'initiative';
fixtures.push({ name: 'initiative-handoff', level: initiative, edit: (state) => {
  state.units.forEach((unit) => { unit.done = true; unit.capture = 17; });
  state.map.captureProgress[0] = 33; state.map.captureProgress[1] = 44;
  state.units[0].directive = { mode: 'patrol', waypoints: [at(1), at(0)], cursor: 0 };
} });
fixtures.push({ name: 'structures-and-overlays', level: make(), edit: (state) => {
  state.structures = [{ id: 'gate', type: 'gate', ...at(2), owner: 0, hp: 100, disabled: false, statuses: [] }];
  state.scenario.overlays = [{ id: 'water', type: 'flooded', cells: [at(3)], remainingRounds: null }];
} });
fixtures.push({ name: 'missing-zone-and-departed-assignee', level: make(), edit: (state) => {
  delete state.scenario.zones.blue;
  state.deployment!.assignments[0].unitIds.push(9999);
} });
fixtures.push({ name: 'no-assignment-for-current-player', level: make(), edit: (state) => { state.currentPlayer = 3; } });
fixtures.push({ name: 'not-deploying-playing', level: make(), edit: (state) => { state.phase = 'playing'; } });
fixtures.push({ name: 'not-deploying-over', level: make(), edit: (state) => { state.phase = 'over'; } });
fixtures.push({ name: 'absent-deployment', level: make(), edit: (state) => { state.deployment = null; } });
const trigger = make();
trigger.scenario!.triggers = [{ id: 'must-not-fire-on-confirm', timing: 'afterAction',
  condition: { type: 'turnAtLeast', turn: 1 }, effects: [{ type: 'emitSignal', signal: 'after-action' }] }];
fixtures.push({ name: 'confirm-skips-post-action-triggers', level: trigger });

function dispatch(state: GameState, action: Action) {
  try {
    const events = engine.dispatch(state, action);
    return { action, stateHash: hashState(state), events, error: null };
  } catch (error) {
    if (!(error instanceof Error)) throw error;
    return { action, stateHash: hashState(state), events: [], error: { name: error.name, message: error.message } };
  }
}
const cases = fixtures.map(({ name, level, edit }) => {
  const initial = createState(engine.rules, level);
  const rawStateHash = hashState(initial);
  edit?.(initial);
  const points = Array.from({ length: 12 }, (_, index) => at(index % 6, Math.floor(index / 6)))
    .concat([at(-1), at(6), at(0, -1), at(99, 99)]);
  const roster = deploymentRoster(initial);
  const queries = initial.units.map((unit) => ({
    unit: unit.id,
    spots: deploymentSpots(engine.rules, initial, unit).map((spot) => ({ at: spot.at, swaps: spot.swaps?.id ?? null })),
    refusals: points.map((target) => ({ at: target, reason: deploymentRefusal(engine.rules, initial, unit, target) })),
  }));
  const actions: Action[] = initial.units.flatMap((unit) => points.map((target) =>
    ({ kind: 'deployUnit' as const, unit: unit.id, at: target })));
  actions.push({ kind: 'deployUnit', unit: 9999, at: at(0) }, { kind: 'finishDeployment' });
  if (initial.phase === 'deployment') actions.push({ kind: 'endTurn' });
  const attempts = actions.map((action) => dispatch(structuredClone(initial), action));
  const progress = structuredClone(initial);
  const confirmations = [];
  for (let guard = 0; progress.phase === 'deployment' && progress.deployment && guard < 8; guard++) {
    const result = dispatch(progress, { kind: 'finishDeployment' });
    confirmations.push(result);
    if (result.error) break;
  }
  return { name, level, rawStateHash, state: initial,
    roster: roster ? { player: roster.player, units: roster.units.map((unit) => unit.id), zone: roster.zone } : null,
    queries, attempts, confirmations };
});

const invalidLevels: { name: string; level: LevelData; error: string }[] = [];
for (const [name, edit] of [
  ['empty-order', (level: LevelData) => { level.deployment!.order = []; }],
  ['unknown-player', (level: LevelData) => { level.deployment!.order = [99]; }],
  ['unknown-zone', (level: LevelData) => { level.deployment!.zones[0].zone = 'missing'; }],
  ['wrong-owner-key', (level: LevelData) => { level.deployment!.zones[0].unitKeys = ['red']; }],
  ['duplicate-key', (level: LevelData) => { level.deployment!.zones[0].unitKeys = ['foot', 'foot']; }],
  ['multiple-assignments', (level: LevelData) => { level.deployment!.zones.push({ player: 1, zone: 'blue' }); }],
  ['ordered-player-without-zone', (level: LevelData) => { level.deployment!.order = [1, 2, 3]; }],
] as const) {
  const level = make(); edit(level);
  let refused = false;
  try { createState(engine.rules, level); }
  catch (error) {
    if (!(error instanceof Error)) throw error;
    refused = true;
    invalidLevels.push({ name, level, error: error.message });
  }
  if (!refused) throw new Error(`Invalid level ${name} was accepted`);
}
writeFileSync('tools/godot/deployment-checks/vectors.json', `${JSON.stringify({ cases, invalidLevels })}\n`);
console.log(`Generated ${cases.length} deployment fixtures, ${cases.reduce((n, c) => n + c.attempts.length, 0)} action outcomes, ${invalidLevels.length} construction refusals.`);
