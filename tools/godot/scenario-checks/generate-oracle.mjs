import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { createState } from '../../../packages/battle-engine/src/state';
import { applyScenarioEffect, conditionMet } from '../../../packages/battle-engine/src/scenario';
import { compositeStatus } from '../../../packages/battle-engine/src/composites';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { objectiveOutcome, evaluateVictory } from '../../../packages/battle-engine/src/victory';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const output = process.env.EMPIRE_SCENARIO_ORACLE_OUTPUT;
if (!output) throw new Error('Set EMPIRE_SCENARIO_ORACLE_OUTPUT to the temporary oracle file.');
const packs = JSON.parse(readFileSync(`${root}/godot/data/content-packs.json`, 'utf8'));
const levels = JSON.parse(readFileSync(`${root}/godot/data/grayflag-campaign-levels.json`, 'utf8'));
const original = JSON.parse(readFileSync(`${root}/godot/tests/oracle/grayflag-initial-raw.json`, 'utf8')).state;
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const rules = createBattleEngine({ content }).rules;
const clone = value => structuredClone(value);
const cases = [];
function base() {
  const state = clone(original);
  state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0),
    captureProgress: Array(49).fill(0), elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
  state.units = original.units.slice(0, 3).map((unit, index) => ({ ...clone(unit), id: index + 1, x: index + 1, y: 1, commanderId: null }));
  state.structures = []; state.composites = []; state.commanders = []; state.markers = []; state.pendingCasts = []; state.embarkedUnits = [];
  state.rules.moraleEnabled = false;
  state.scenario.variables = {}; state.scenario.zones = { near: [{ x: 1, y: 1 }, { x: 2, y: 1 }] };
  state.scenario.overlays = []; state.scenario.engagementRules = [];
  return state;
}
function run(name, input, effects = [], condition = null, composite = null, objective = null, victory = false) {
  const state = clone(input), initial = clone(input), events = [], hashes = [];
  const emit = event => { events.push(event); hashes.push(hashState(state)); };
  let result = null, error = null;
  try {
    for (const effect of effects) applyScenarioEffect(rules, state, effect, emit);
    if (condition) result = conditionMet(rules, state, condition);
    if (composite) result = compositeStatus(state, composite);
    if (objective) result = { outcome: objectiveOutcome(rules, state, 1, objective), description: rules.objectives.describe(objective) };
    if (victory) result = evaluateVictory(rules, state, emit);
  } catch (caught) {
    if (!['DomainInvariantError', 'IllegalActionError'].includes(caught.name)) throw caught;
    error = { kind: caught.name.replace('Error', ''), message: caught.message };
  }
  cases.push({ name, initial, effects, condition, composite, objective, victory, result, error, events, hashes, after: state });
}
for (const level of levels) {
  const state = createState(rules, level);
  for (const trigger of level.scenario?.triggers ?? [])
    run(`authored ${level.id} ${trigger.id}`, state, trigger.effects);
}
for (const value of [true, false, 'pending', 12.5, null]) run(`set variable ${value}`, base(), [{ type: 'setVariable', key: 'flag', value }]);
for (const initial of [undefined, null, false, '12', -1, 1.5]) {
  const state = base(); if (initial !== undefined) state.scenario.variables.score = initial;
  run(`add variable initial=${initial}`, state, [{ type: 'addVariable', key: 'score', amount: 2.5 }]);
}
for (const selector of [{}, { ids: [2] }, { keys: [original.units[0].key] }, { zone: 'near' }, { owner: 2 }, { anyTags: ['archer', 'infantry'] }]) {
  run(`change owner selector ${JSON.stringify(selector)}`, base(), [{ type: 'changeUnitOwner', selector, owner: 2 }]);
}
run('same owner preserves readiness', base(), [{ type: 'changeUnitOwner', selector: { ids: [1] }, owner: 1 }]);
{
  const state = base(); state.scenario.engagementRules = [{ id: 'same', zone: 'near', mode: 'no-attacks' }, { id: 'same', zone: 'near', mode: 'no-hostile' }];
  run('remove engagement first duplicate then absent', state, [{ type: 'removeEngagementRule', id: 'same' }, { type: 'removeEngagementRule', id: 'absent' }]);
}
const arrival = (key = 'arrival', x = 2, y = 3, owner = 1) => ({ key, unit: 'c01.swordsman', owner, x, y });
for (const ready of [false, true]) run(`spawn ready=${ready}`, base(), [{ type: 'spawnUnits', ready, units: [arrival()] }]);
{
  const state = base();
  run('spawn occupied skips only occupied entry', state, [{ type: 'spawnUnits', units: [arrival('occupied', 1, 1), arrival('open', 2, 3)] }]);
  const rich = { ...arrival(), hp: 999, morale: 12.5,
    rank: 2, rankProgress: 320, reaction: 'guard', facing: 'north', formation: 'formation-defensive', resources: { momentum: { current: 73, capacity: 150 } },
    career: 'militia', unlockedCareers: ['militia'], learnedAbilities: ['heal'], directive: { mode: 'patrol', zone: 'near', waypoints: [{ x: 1, y: 1 }], cursor: 1.5 } };
  run('spawn invalid career allocation ordering', state, [{ type: 'spawnUnits', ready: true, reason: 'test', units: [rich] }]);
  run('spawn rich valid source fields', state, [{ type: 'spawnUnits', ready: true, reason: 'test', units: [{ ...rich, unit: 'soldier' }] }]);
  run('spawn duplicate active key', state, [{ type: 'spawnUnits', units: [arrival(state.units[0].key)] }]);
  state.markers = [{ id: 1, kind: 'corpse', at: { x: 4, y: 4 }, fallenUnit: { ...clone(state.units[0]), key: 'fallen' } }];
  run('spawn reserved fallen key', state, [{ type: 'spawnUnits', units: [arrival('fallen')] }]);
  run('spawn duplicate siblings partial effect', base(), [{ type: 'spawnUnits', units: [arrival('same', 2, 3), arrival('same', 3, 3)] }]);
  run('spawn missing owner precedes out of bounds', base(), [{ type: 'spawnUnits', units: [arrival('missing', -1, -1, 999)] }]);
  run('spawn out of bounds', base(), [{ type: 'spawnUnits', units: [arrival('outside', -1, -1)] }]);
  const water = base(); water.map.tiles[23] = 'water';
  run('spawn impassable ground', water, [{ type: 'spawnUnits', units: [arrival()] }]);
  const hex = base(); hex.rules.grid = 'hex'; run('spawn hex resting facing', hex, [{ type: 'spawnUnits', units: [arrival()] }]);
}
for (const directive of [{ mode: 'assault' }, { mode: 'guard', zone: 'near' }, { mode: 'patrol', waypoints: [{ x: 1, y: 1 }], cursor: -0.5 }, { mode: 'retreat', zone: 'missing' }])
  run(`directive ${JSON.stringify(directive)}`, base(), [{ type: 'setUnitDirective', selector: { owner: 1 }, directive }]);
for (const enabled of [false, true]) {
  const state = base(); state.rules.moraleEnabled = enabled;
  run(`scenario morale routing enabled=${enabled}`, state, [{ type: 'changeMorale', selector: {}, amount: -999 }]);
}
function compositeState() {
  const state = base(); state.units = [];
  state.structures = [0, 1, 2].map(index => ({ id: `part-${index}`, type: 'boss_part', x: index + 1, y: 3, owner: 2, hp: 180, disabled: false, statuses: [] }));
  state.composites = [{ id: 'boss', parts: ['part-0', 'part-1', 'part-2'], minimumNeutralized: 2, tags: [] }];
  return state;
}
for (const [name, mutate] of [
  ['intact', () => {}], ['damaged', state => state.structures[0].disabled = true],
  ['neutralized', state => { state.structures[0].hp = 0; state.structures[1].disabled = true; }],
  ['missing part', state => state.structures.pop()],
]) {
  const state = compositeState(); mutate(state);
  run(`composite status ${name}`, state, [], null, 'boss');
  run(`composite condition ${name}`, state, [], { type: 'composite', id: 'boss', state: 'neutralized' });
}
for (const [dx, dy] of [[1, 0], [0.5, -0.5], [0, 0], [99, 0]])
  run(`composite move ${dx},${dy}`, compositeState(), [{ type: 'moveComposite', id: 'boss', dx, dy }]);
{
  const state = compositeState(); state.structures[0].hp = 0;
  run('composite destroyed part stays behind', state, [{ type: 'moveComposite', id: 'boss', dx: 1, dy: 0 }]);
  state.units = [{ ...clone(original.units[0]), x: 3, y: 4 }];
  run('composite collision with unit atomic', state, [{ type: 'moveComposite', id: 'boss', dx: 0, dy: 1 }]);
  const occupied = compositeState(); occupied.structures.push({ id: 'foreign', type: 'gate', owner: 0, x: 4, y: 3, hp: 10, disabled: false, statuses: [] });
  run('composite collision with foreign structure atomic', occupied, [{ type: 'moveComposite', id: 'boss', dx: 1, dy: 0 }]);
  const overlap = compositeState(); overlap.structures[1].x = overlap.structures[0].x;
  run('composite overlapping parts rejected', overlap, [{ type: 'moveComposite', id: 'boss', dx: 0, dy: 1 }]);
  run('unknown composite rejected', compositeState(), [], null, 'absent');
}
function objectiveState(objective, status = 'active') {
  const state = compositeState();
  state.units = [...base().units, { ...clone(original.units[0]), id: 99, owner: 2, x: 5, y: 1, commanderId: null }];
  for (const player of state.players) { player.objectives = []; player.objectiveStates = {}; }
  const owner = state.players.find(player => player.id === 1);
  owner.objectives = [objective];
  function record(value) {
    if (value.id) owner.objectiveStates[value.id] = { id: value.id, status, hidden: false };
    for (const child of value.objectives ?? []) record(child);
    if (value.objective) record(value.objective);
  }
  record(objective);
  return state;
}
for (const destroyed of [0, 1, 2, 3]) for (const threshold of [undefined, 1, 3]) {
  const objective = { id: 'boss-goal', type: 'neutralizeComposite', composite: 'boss', ...(threshold === undefined ? {} : { minimumNeutralized: threshold }) };
  const state = objectiveState(objective); for (let i = 0; i < destroyed; i++) state.structures[i].hp = 0;
  run(`composite objective ${destroyed} destroyed threshold=${threshold}`, state, [], null, null, objective);
  run(`composite victory ${destroyed} destroyed threshold=${threshold}`, state, [], null, null, null, true);
}
for (const status of ['active', 'completed', 'failed', 'inactive', 'cancelled']) {
  const objective = { id: 'terminal', type: 'neutralizeComposite', composite: 'absent' };
  run(`composite objective terminal ${status}`, objectiveState(objective, status), [], null, null, objective);
}
for (const type of ['optional', 'failOn', 'all', 'any', 'sequence']) {
  const child = { id: 'nested', type: 'neutralizeComposite', composite: 'boss' };
  const objective = { id: 'wrapper', type, ...(type === 'optional' || type === 'failOn' ? { objective: child } : { objectives: [child] }),
    ...(type === 'failOn' ? { condition: { type: 'variable', key: 'fail', op: 'eq', value: true } } : {}) };
  const state = objectiveState(objective); state.structures[0].disabled = true; state.structures[1].hp = 0;
  run(`nested composite description ${type}`, state, [], null, null, objective);
  run(`nested composite victory ${type}`, state, [], null, null, null, true);
}
{
  const objective = { id: 'labelled', type: 'neutralizeComposite', composite: 'boss', label: '自定义目标' };
  const state = objectiveState(objective); state.structures[0].disabled = true; state.structures[1].hp = 0;
  run('composite custom label wins', state, [], null, null, objective);
  run('composite custom label victory reason', state, [], null, null, null, true);
}
writeFileSync(output, JSON.stringify({ packs, cases }));
console.log(`Wrote ${cases.length} live TypeScript scenario cases across ${levels.length} authored levels`);
