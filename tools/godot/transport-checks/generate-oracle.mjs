import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { spawnUnit } from '../../../packages/battle-engine/src/state';
import { passengersOf, embarkRefusal, carrierOptions, embarkUnit, disembarkRefusal, passengerOptions, disembarkUnit } from '../../../packages/battle-engine/src/transports';
import { returnUnitToField } from '../../../packages/battle-engine/src/unit-return';
import { resolveDamage } from '../../../packages/battle-engine/src/damage';
import { changeMorale, surrenderUnit } from '../../../packages/battle-engine/src/morale';
import { applyScenarioEffect } from '../../../packages/battle-engine/src/scenario';
import { hashState } from '../../../packages/battle-engine/src/replay';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const output = process.env.EMPIRE_TRANSPORT_ORACLE_OUTPUT;
if (!output) throw new Error('Set EMPIRE_TRANSPORT_ORACLE_OUTPUT to the temporary oracle file.');
const packs = JSON.parse(readFileSync(`${root}/godot/data/content-packs.json`, 'utf8'));
const original = JSON.parse(readFileSync(`${root}/godot/tests/oracle/grayflag-initial-raw.json`, 'utf8')).state;
const clone = value => structuredClone(value);
function compose(patches = [], engine = false) {
  const sourcePacks = clone(packs);
  for (const patch of patches) Object.assign(sourcePacks.flatMap(pack => pack[patch.family] ?? []).find(entry => entry.id === patch.id), patch.values);
  const content = createContentCatalog(); new ContentPackInstaller(content).install(...sourcePacks);
  const composed = createBattleEngine({ content });
  return engine ? composed : composed.rules;
}
const defaults = compose();
function base() {
  const state = clone(original);
  state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0), captureProgress: Array(49).fill(0),
    elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
  state.units = []; state.nextUnitId = 1; state.nextMarkerId = 1; state.structures = []; state.composites = [];
  state.embarkedUnits = []; state.markers = []; state.commanders = []; state.pendingCasts = [];
  state.scenario.overlays = []; state.scenario.zones = {}; state.scenario.engagementRules = [];
  state.rules.moraleEnabled = false;
  for (const player of state.players) {
    player.objectives = [{ id: 'route', type: 'routEnemies', label: 'Defeat enemies' }];
    player.objectiveStates = { route: { id: 'route', status: 'active', hidden: false } };
  }
  for (const [type, owner, x, y] of [
    ['c01.swordsman', 1, 3, 3], ['c01.supply-wagon', 1, 4, 3], ['c01.archer', 1, 4, 4],
    ['c01.supply-wagon', 1, 3, 2], ['c01.supply-wagon', 2, 3, 4],
  ]) spawnUnit(defaults, state, type, owner, { x, y }, { source: { key: `test-${state.nextUnitId}` } });
  state.units[0].capture = 37; state.map.captureProgress[24] = 37;
  return state;
}
function aboard() { const state = base(); embarkUnit(defaults, state, 1, 2, () => {}); embarkUnit(defaults, state, 3, 2, () => {}); return state; }
const cases = [];
function run(name, input, steps, patches = []) {
  const state = clone(input), initial = clone(input), rules = compose(patches), events = [], hashes = [];
  const emit = event => { events.push(event); hashes.push(hashState(state)); };
  let result = null, error = null;
  try {
    for (const step of steps) {
      const unit = state.units.find(unit => unit.id === (step.unit ?? 1));
      const carrier = state.units.find(unit => unit.id === (step.carrier ?? 2));
      if (step.op === 'query') result = { passengers: passengersOf(state, step.carrier ?? 2), carriers: carrierOptions(rules, state, unit), passengersOptions: passengerOptions(rules, state, carrier) };
      else if (step.op === 'embarkRefusal') result = embarkRefusal(rules, state, unit, carrier);
      else if (step.op === 'disembarkRefusal') result = disembarkRefusal(rules, state, step.carrier ?? 2, step.unit ?? 1, step.at);
      else if (step.op === 'embark') embarkUnit(rules, state, step.unit ?? 1, step.carrier ?? 2, emit);
      else if (step.op === 'disembark') disembarkUnit(rules, state, step.carrier ?? 2, step.unit ?? 1, step.at, emit);
      else if (step.op === 'damage') result = resolveDamage(rules, state, { unit: step.unit ?? 2, amount: step.amount, report: blow => ({ type: 'statusTick', unit: step.unit ?? 2, status: 'poisoned', amount: blow.amount, hpAfter: blow.hpAfter }) }, emit);
      else if (step.op === 'morale') result = changeMorale(rules, state, step.unit ?? 2, step.amount, 'test', emit);
      else if (step.op === 'surrender') result = surrenderUnit(rules, state, step.unit ?? 2, step.to, emit);
      else if (step.op === 'withdraw') applyScenarioEffect(rules, state, { type: 'withdrawUnits', selector: { ids: [step.unit ?? 2] }, leaveCorpse: step.leaveCorpse }, emit);
      else if (step.op === 'return') {
        const marker = step.externalMarker ?? state.markers.find(marker => marker.id === step.marker);
        result = returnUnitToField(state, marker, { at: step.at, owner: step.owner, hp: step.hp }, emit);
      } else if (step.op === 'embarkClone') {
        const previous = unit;
        embarkUnit(rules, state, previous.id, step.carrier ?? 2, emit);
        const snapshot = passengersOf(state, step.carrier ?? 2).find(unit => unit.id === previous.id);
        snapshot.morale.current = 1; snapshot.statuses[0].remaining = 99;
        snapshot.resources.momentum.current = 2; snapshot.weaponState['c01.border-blade'].cooldownRemaining = 3;
        snapshot.career.mastery.test = 100; snapshot.directive.waypoints[0].x = 6; snapshot.learnedAbilities.push('heal');
        result = previous;
      } else throw new Error(`Unknown test operation ${step.op}`);
    }
    result = result === undefined ? null : JSON.parse(JSON.stringify(result));
  } catch (caught) {
    if (!['DomainInvariantError', 'IllegalActionError'].includes(caught.name)) throw caught;
    error = { kind: caught.name.replace('Error', ''), message: caught.message };
  }
  cases.push({ name, initial, steps, patches, result, error, events, hashes, after: state });
}
const wagonPatch = transport => [{ family: 'units', id: 'c01.supply-wagon', values: { transport } }];
run('base carrier options state order', base(), [{ op: 'query' }]);
{
  const state = base(); state.units = [state.units[0], state.units[3], state.units[1], state.units[4], state.units[2]];
  run('carrier options preserve state order not coordinate order', state, [{ op: 'query' }]);
  state.players.find(player => player.id === 2).team = 1;
  run('allied other owner included in queries', state, [{ op: 'query' }]);
}
for (const [name, unit, carrier] of [['self', 2, 2], ['noncarrier', 1, 3], ['enemy', 1, 5], ['not adjacent', 4, 2], ['missing passenger', 99, 2], ['missing carrier', 1, 99]])
  run(`embark ${name}`, base(), [{ op: 'embark', unit, carrier }]);
{
  const state = base(); state.units.find(unit => unit.id === 4).x = 4;
  run('embark refuses adjacent carrier passenger', state, [{ op: 'embark', unit: 4 }]);
}
run('boarding ordinary passenger', base(), [{ op: 'embark' }]);
run('boarding then landing preserves carrier readiness', base(), [{ op: 'embark' }, { op: 'disembark', at: { x: 4, y: 2 } }]);
run('board second passenger and ordered passenger queries', base(), [{ op: 'embark' }, { op: 'embark', unit: 3 }, { op: 'query', unit: 4 }]);
{
  const state = aboard(); state.units.find(unit => unit.id === 4).x = 4;
  run('full carrier refusal precedes carrier passenger refusal', state, [{ op: 'embark', unit: 4 }]);
}
for (const [name, profile] of [
  ['allowed mismatch', { capacity: 2, allowedTags: ['flying'] }],
  ['forbidden overrides allowed', { capacity: 2, allowedTags: ['infantry'], forbiddenTags: ['infantry'] }],
  ['empty allow list accepts', { capacity: 2, allowedTags: [] }],
]) run(`profile ${name}`, base(), [{ op: 'query' }, { op: 'embark' }], wagonPatch(profile));
{
  const state = base(); state.units[0].done = true; state.units[1].done = true;
  run('transport rule leaves entitlement to dispatcher', state, [{ op: 'embark' }, { op: 'disembark', at: { x: 4, y: 2 } }]);
  const nested = base(); const unit = nested.units[0];
  unit.statuses = [{ id: 'poisoned', remaining: 2, stacks: 1 }]; unit.resources = { momentum: { current: 50, capacity: 150 } };
  unit.directive = { mode: 'patrol', cursor: 0, waypoints: [{ x: 2, y: 2 }] };
  run('boarding deeply isolates passenger snapshot', nested, [{ op: 'embarkClone' }]);
}
for (const grid of ['square4', 'square8', 'hex']) {
  const state = base(); state.rules.grid = grid;
  run(`${grid} embark and passenger spots`, state, [{ op: 'embark' }, { op: 'query', unit: 3 }]);
  const diagonal = base(); diagonal.rules.grid = grid; diagonal.units[1].x = 4; diagonal.units[1].y = 4; diagonal.units[2].x = 6;
  run(`${grid} diagonal boarding`, diagonal, [{ op: 'embark' }]);
  const edge = aboard(); edge.rules.grid = grid; edge.units.find(unit => unit.id === 2).x = 0; edge.units.find(unit => unit.id === 2).y = 0;
  run(`${grid} landing edge clipping`, edge, [{ op: 'query', unit: 4 }]);
}
for (const [name, carrier, unit, at] of [
  ['missing carrier', 99, 1, { x: 4, y: 2 }], ['noncarrier', 3, 1, { x: 4, y: 2 }],
  ['wrong carrier', 4, 1, { x: 3, y: 3 }], ['missing passenger', 2, 99, { x: 4, y: 2 }],
  ['outside board', 2, 1, { x: -1, y: 3 }], ['not adjacent', 2, 1, { x: 0, y: 0 }],
  ['occupied', 2, 1, { x: 3, y: 3 }], ['valid', 2, 1, { x: 4, y: 2 }],
]) {
  const state = aboard();
  if (name === 'occupied') state.units.find(unit => unit.id === 5).y = 3;
  run(`landing query ${name}`, state, [{ op: 'disembarkRefusal', carrier, unit, at }]);
  run(`landing commit ${name}`, state, [{ op: 'disembark', carrier, unit, at }]);
}
{
  const water = aboard(); water.map.tiles[18] = 'water';
  run('landing blocked terrain', water, [{ op: 'disembark', at: { x: 4, y: 2 } }]);
  const blocker = aboard(); blocker.structures = [{ id: 'wall', type: 'bulkhead', owner: 0, x: 4, y: 2, hp: 120, disabled: false, statuses: [] }];
  run('landing blocked structure', blocker, [{ op: 'disembark', at: { x: 4, y: 2 } }]);
  const cliff = aboard(); cliff.map.elevation[18] = 9; cliff.map.cliffs = [{ from: { x: 4, y: 3 }, to: { x: 4, y: 2 } }];
  run('landing ignores intermediate elevation and cliffs', cliff, [{ op: 'disembark', at: { x: 4, y: 2 } }]);
  const ownership = aboard(); ownership.units.find(unit => unit.id === 2).owner = 2;
  run('landing does not transfer passenger allegiance', ownership, [{ op: 'disembark', at: { x: 4, y: 2 } }]);
  const waiting = aboard(); const passenger = waiting.embarkedUnits[0].unit;
  passenger.morale.current = 1; passenger.statuses = [{ id: 'poisoned', remaining: 1, stacks: 1 }]; passenger.reactionUsedRound = waiting.turn;
  run('disembark preserves morale status and spent reaction', waiting, [{ op: 'disembark', at: { x: 4, y: 2 } }]);
}
for (const op of ['damage', 'morale', 'surrender', 'withdraw']) {
  const state = aboard(); state.rules.moraleEnabled = true;
  state.pendingCasts = [{ caster: 1, owner: 1, ability: 'attack', weapon: 'c01.border-blade', target: { x: 0, y: 0 }, origin: { x: 3, y: 3 }, declaredAt: 0, resolveAt: 2 },
    { caster: 2, owner: 1, ability: 'attack', weapon: 'c01.border-blade', target: { x: 0, y: 0 }, origin: { x: 4, y: 3 }, declaredAt: 0, resolveAt: 2 }];
  run(`carrier ${op} canonical passenger departure`, state, [{ op, unit: 2, amount: op === 'morale' ? -999 : 999, to: 2, leaveCorpse: false }]);
}
run('withdraw carrier corpse still withdraws passengers alive', aboard(), [{ op: 'withdraw', unit: 2, leaveCorpse: true }]);
{
  const state = aboard(); const commander = clone(original.commanders[0]); commander.unitId = 2; commander.owner = 1;
  state.commanders = [commander]; state.units.find(unit => unit.id === 4).commanderId = commander.id;
  run('carrier commander death uses canonical aftermath', state, [{ op: 'damage', unit: 2, amount: 999 }]);
}
function returnState(kind = 'transport-loss') {
  const state = base(); const unit = clone(state.units[0]); state.units.shift();
  unit.statuses = [{ id: 'poisoned', remaining: 3, stacks: 1 }]; unit.morale.current = 0; unit.reactionUsedRound = state.turn; unit.done = false;
  state.markers = [{ id: 7, kind, at: { x: 6, y: 6 }, owner: unit.owner, fallenUnit: unit }];
  state.commanders = [{ ...clone(original.commanders[0]), unitId: unit.id, owner: unit.owner }];
  return state;
}
for (const kind of ['corpse', 'transport-loss', 'withdrawn', 'routed', 'surrendered'])
  for (const hp of [undefined, -5, 0.5, 999])
    run(`return ${kind} hp=${hp}`, returnState(kind), [{ op: 'return', marker: 7, at: { x: 0, y: 0 }, hp, owner: 2 }]);
{
  const state = returnState(); state.markers[0].fallenUnit.id = 1000;
  run('return advances next identity', state, [{ op: 'return', marker: 7, at: { x: 0, y: 0 } }]);
  run('return occupied tile refuses without changes', returnState(), [{ op: 'return', marker: 7, at: { x: 4, y: 3 } }]);
  const duplicate = returnState(); duplicate.units[0].id = 1;
  run('return duplicate identity refuses', duplicate, [{ op: 'return', marker: 7, at: { x: 0, y: 0 } }]);
  const duplicateKey = returnState(); duplicateKey.units[0].key = 'test-1';
  run('return duplicate key refuses', duplicateKey, [{ op: 'return', marker: 7, at: { x: 0, y: 0 } }]);
  const empty = returnState(); delete empty.markers[0].fallenUnit;
  run('return marker without unit refuses', empty, [{ op: 'return', marker: 7, at: { x: 0, y: 0 } }]);
  const external = returnState(); const marker = external.markers.pop();
  run('return external marker matches source removal behavior', external, [{ op: 'return', externalMarker: marker, at: { x: 0, y: 0 } }]);
}
const dispatchCases = [];
function dispatchCase(name, state, actions, expectedSuccess = []) {
  const engine = compose([], true), initial = clone(state), receipts = [];
  for (const [index, action] of actions.entries()) {
    let events = [], error = null;
    const before = clone(state);
    try { events = engine.dispatch(state, action); }
    catch (caught) {
      if (!['DomainInvariantError', 'IllegalActionError'].includes(caught.name)) throw caught;
      error = { kind: caught.name.replace('Error', ''), message: caught.message };
    }
    if (expectedSuccess.includes(index) && error) throw new Error(`${name} step ${index} must succeed, got ${error.message}`);
    receipts.push({ action, before, events, error, after: clone(state), hash: hashState(state) });
  }
  dispatchCases.push({ name, initial, receipts });
}
const boardAction = { kind: 'embark', unit: 1, carrier: 2 };
const landAction = { kind: 'disembark', unit: 1, carrier: 2, at: { x: 4, y: 2 } };
dispatchCase('full dispatch board land wait and next turn', base(), [boardAction, landAction, { kind: 'endTurn' }], [0, 1, 2]);
dispatchCase('full dispatch second landing forbidden after carrier done', base(), [boardAction, { kind: 'embark', unit: 3, carrier: 2 }, landAction,
  { kind: 'disembark', unit: 3, carrier: 2, at: { x: 3, y: 3 } }], [0, 1, 2]);
for (const [name, mutate] of [
  ['passenger done', state => { state.units[0].done = true; }],
  ['carrier done boarding is allowed', state => { state.units[1].done = true; }],
  ['enemy passenger', state => { state.units[0].owner = 2; }],
  ['allied other owner carrier refused by dispatcher', state => { state.units[1].owner = 2; state.players[1].team = 1; }],
  ['deployment rejects transport', state => { state.phase = 'deployment'; }],
  ['over rejects transport', state => { state.phase = 'over'; }],
  ['blocked passenger on initiative order', state => { state.rules.turnOrder = 'initiative'; state.turnOrder.policy = 'initiative'; state.turnOrder.activeUnit = 3; }],
]) {
  const state = base(); mutate(state); dispatchCase(name, state, [boardAction]);
}
for (const [name, mutate, action] of [
  ['done carrier cannot land', state => { state.units.find(unit => unit.id === 2).done = true; }, landAction],
  ['enemy carrier cannot land', state => { state.units.find(unit => unit.id === 2).owner = 2; }, landAction],
  ['occupied landing rolls back all state', state => { state.units.find(unit => unit.id === 5).y = 3; }, { ...landAction, at: { x: 3, y: 3 } }],
  ['missing carrier dispatch precedence', () => {}, { ...landAction, carrier: 99 }],
  ['missing passenger dispatch precedence', () => {}, { ...boardAction, unit: 99 }],
]) { const state = aboard(); mutate(state); dispatchCase(name, state, [action]); }
{
  const state = base(); state.rules.turnOrder = 'initiative'; state.turnOrder = { policy: 'initiative', activeUnit: 1, data: { 'charge:1': 100 } };
  dispatchCase('initiative actor boarding and handoff', state, [boardAction, { kind: 'endTurn' }], [0, 1]);
  const landing = aboard(); landing.rules.turnOrder = 'initiative'; landing.turnOrder = { policy: 'initiative', activeUnit: 2, data: { 'charge:2': 100 } };
  dispatchCase('initiative carrier landing and handoff', landing, [landAction, { kind: 'endTurn' }], [0, 1]);
  const blocked = aboard(); blocked.rules.turnOrder = 'initiative'; blocked.turnOrder = { policy: 'initiative', activeUnit: 4, data: { 'charge:4': 100 } };
  dispatchCase('initiative inactive carrier cannot land', blocked, [landAction]);
}
const saveCases = [];
for (const name of ['single passenger', 'two passengers', 'carrier owner changed', 'living unit with historical fallen snapshot']) {
  const state = name === 'single passenger' ? base() : aboard();
  if (name === 'single passenger') embarkUnit(defaults, state, 1, 2, () => {});
  if (name === 'carrier owner changed') state.units.find(unit => unit.id === 2).owner = 2;
  if (name === 'living unit with historical fallen snapshot') {
    const fallenUnit = clone(state.embarkedUnits[0].unit); fallenUnit.hp = 0;
    state.markers.push({ id: 999, kind: 'corpse', at: { x: 1, y: 1 }, owner: 1, fallenUnit }); state.nextMarkerId = 1000;
  }
  const engine = compose([], true);
  const document = engine.saveBattle(state, '2026-10-20T00:00:00.000Z');
  const resumed = engine.loadBattle(JSON.parse(JSON.stringify(document)));
  const actions = name === 'carrier owner changed' ? [{ kind: 'endTurn' }, landAction] : [landAction, { kind: 'endTurn' }];
  const receipts = actions.map(action => {
    const before = clone(resumed), events = engine.dispatch(resumed, action);
    return { action, before, events, after: clone(resumed), hash: hashState(resumed) };
  });
  saveCases.push({ name, document, receipts });
}
writeFileSync(output, JSON.stringify({ packs, cases, dispatchCases, saveCases }));
console.log(`Wrote ${cases.length} live TypeScript transport/departure/return cases and ${dispatchCases.length} full-dispatch cases`);
