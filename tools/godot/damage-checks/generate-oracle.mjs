import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { resolveDamage } from '../../../packages/battle-engine/src/damage';
import { StatusLifecycleContext } from '../../../packages/battle-engine/src/statuses';
import { changeMorale, surrenderUnit } from '../../../packages/battle-engine/src/morale';
import { applyScenarioEffect } from '../../../packages/battle-engine/src/scenario';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const output = process.env.EMPIRE_DAMAGE_ORACLE_OUTPUT;
if (!output) throw new Error('Set EMPIRE_DAMAGE_ORACLE_OUTPUT to the temporary oracle file.');
const packs = JSON.parse(readFileSync(`${root}/godot/data/content-packs.json`, 'utf8'));
const content = createContentCatalog();
new ContentPackInstaller(content).install(...packs);
const rules = createBattleEngine({ content }).rules;
const original = JSON.parse(readFileSync(`${root}/godot/tests/oracle/grayflag-initial-raw.json`, 'utf8')).state;

function base() {
  const state = structuredClone(original);
  const unit = state.units[0];
  state.units = [0, 1, 2].map((index) => ({
    ...structuredClone(unit), id: index + 1, x: index, y: 1,
    owner: index === 2 ? 2 : 1, commanderId: null,
  }));
  state.commanders = [];
  state.pendingCasts = [];
  state.markers = [];
  state.embarkedUnits = [];
  state.nextMarkerId = 1;
  state.rules.moraleEnabled = false;
  state.map.captureProgress.fill(37);
  return state;
}

function commander(state) {
  state.units[1].commanderId = 'test';
  state.commanders = [{ ...structuredClone(original.commanders[0]), id: 'test', unitId: 1, owner: 1 }];
  return state;
}

function passengers(state) {
  state.embarkedUnits = [4, 5].map((id) => ({
    carrier: 1, unit: { ...structuredClone(state.units[1]), id },
  }));
  return state;
}

function cast(state) {
  state.pendingCasts = [{
    caster: 1, owner: 1, ability: 'attack', weapon: 'c01.border-blade',
    target: { x: 2, y: 1 }, origin: { x: 0, y: 1 }, declaredAt: 0, resolveAt: 2,
  }];
  return state;
}

function trace(state) {
  return {
    units: state.units.map((unit) => unit.id),
    markers: state.markers.map((marker) => marker.id),
    passengers: state.embarkedUnits.map((entry) => entry.unit.id),
    casts: state.pendingCasts.map((entry) => entry.caster),
    capture: state.map.captureProgress[state.map.width],
  };
}

const cases = [];
function run(name, state, op, args) {
  const initial = structuredClone(state);
  const events = [];
  const traces = [];
  const emit = (event) => { events.push(event); traces.push(trace(state)); };
  let outcome;
  switch (op) {
    case 'resolve':
      outcome = resolveDamage(rules, state, {
        unit: args.unit, amount: args.amount, nonlethal: args.nonlethal,
        report: (blow) => ({
          type: 'statusTick', unit: args.unit, status: 'poisoned', amount: blow.amount, hpAfter: blow.hpAfter,
        }),
      }, emit);
      break;
    case 'status':
      outcome = new StatusLifecycleContext(rules, state, { ...state.units[0], id: args.unit },
        { id: 'poisoned', remaining: 1, stacks: 1 }, emit).damage(args.amount, args.nonlethal);
      break;
    case 'morale':
      outcome = changeMorale(rules, state, args.unit, args.amount, 'test', emit);
      break;
    case 'surrender':
      outcome = surrenderUnit(rules, state, args.unit, args.to, emit);
      break;
    case 'withdraw':
      applyScenarioEffect(rules, state, {
        type: 'withdrawUnits', selector: { ids: [args.unit] }, leaveCorpse: args.leaveCorpse,
      }, emit);
      break;
    default: throw new Error(`Unknown operation: ${op}`);
  }
  cases.push({ name, initial, op, args, outcome: outcome ?? null, events, traces, after: state });
}

for (const amount of [-100, -0.5, 0, 0.49, 0.5, 1, 10.5, 116, 999]) {
  run(`damage ${amount}`, base(), 'resolve', { unit: 1, amount, nonlethal: false });
}
run('absent', base(), 'resolve', { unit: 999, amount: 999, nonlethal: false });
for (const hp of [1, 8, 116]) {
  const state = base();
  state.units[0].hp = hp;
  run(`nonlethal hp ${hp}`, state, 'resolve', { unit: 1, amount: 999, nonlethal: true });
}
run('commander transport death', cast(passengers(commander(base()))), 'resolve', {
  unit: 1, amount: 999, nonlethal: false,
});
{
  const state = cast(passengers(commander(base())));
  state.rules.moraleEnabled = true;
  state.rules.moraleDamageFactor = 40;
  run('morale rout', state, 'resolve', { unit: 1, amount: 20, nonlethal: false });
}
for (const amount of [-999, -25, -1, -0.5, 0, 0.5, 999]) {
  const state = base();
  state.rules.moraleEnabled = true;
  state.units[0].morale.current = 60;
  run(`morale ${amount}`, state, 'morale', { unit: 1, amount });
}
for (const to of [undefined, 2]) {
  run(`surrender ${to}`, cast(passengers(commander(base()))), 'surrender', { unit: 1, to });
}
for (const leaveCorpse of [false, true]) {
  run(`withdraw ${leaveCorpse}`, cast(passengers(commander(base()))), 'withdraw', { unit: 1, leaveCorpse });
}
for (const grid of ['square4', 'square8', 'hex']) {
  const state = base();
  state.rules.grid = grid;
  state.rules.moraleEnabled = true;
  state.units[1].x = 2;
  state.units[1].y = 3;
  run(`mourning ${grid}`, state, 'resolve', { unit: 1, amount: 999, nonlethal: false });
}
{
  const state = cast(passengers(commander(base())));
  state.rules.moraleEnabled = true;
  state.units[1].morale.current = 10;
  run('commander follower routed', state, 'resolve', { unit: 1, amount: 999, nonlethal: false });
}
for (const [unit, nonlethal] of [[1, true], [1, false], [999, false]]) {
  run(`status ${unit} ${nonlethal}`, base(), 'status', { unit, amount: 999, nonlethal });
}
writeFileSync(output, JSON.stringify(cases));
console.log(`Wrote ${cases.length} damage/departure oracle cases`);
