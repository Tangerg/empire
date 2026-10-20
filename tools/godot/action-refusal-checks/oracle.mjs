import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { spawnUnit } from '../../../packages/battle-engine/src/state';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { playerResource, unitResource, weaponResource } from '../../../packages/battle-engine/src/resources';
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const original = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const clone = value => structuredClone(value), cases = [], resources = [];
function base(type = 'c01.swordsman') {
  const state = clone(original);
  state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0), captureProgress: Array(49).fill(0), elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
  state.units = []; state.structures = []; state.commanders = []; state.composites = []; state.markers = []; state.embarkedUnits = []; state.pendingCasts = []; state.nextUnitId = 1;
  state.rules.moraleEnabled = false; state.scenario = { ...state.scenario, variables: {}, zones: {}, triggers: [], firedTriggerIds: [], triggerRuntime: {}, eventCounts: {}, overlays: [], engagementRules: [] };
  for (const player of state.players) { player.alive = true; player.objectives = [{ id: 'route', type: 'routEnemies' }]; player.objectiveStates = { route: { id: 'route', status: 'active', hidden: false } }; }
  spawnUnit(engine.rules, state, type, 1, { x: 1, y: 1 }, { done: false });
  spawnUnit(engine.rules, state, 'c01.swordsman', 2, { x: 6, y: 6 }, { done: false });
  return state;
}
function run(name, state, command, edit = () => {}, success = false, path = [{ x: 1, y: 1 }]) {
  edit(state); const initial = clone(state), action = { kind: 'command', unit: 1, path, command }; let error = null, events = [];
  try { events = engine.dispatch(state, action); } catch (caught) { if (!['IllegalActionError', 'DomainInvariantError'].includes(caught.name)) throw caught; error = { kind: caught.name.replace('Error', ''), message: caught.message }; }
  if (success === Boolean(error)) throw new Error(`${name}: expected ${success ? 'success' : 'refusal'}, got ${JSON.stringify(error)}`);
  cases.push({ name, initial, action, error, events, after: state, hash: hashState(state) });
}
run('unknown ability', base(), { ability: 'missing' });
run('unit lacks attack before weapon validation', base('c01.refugee'), { ability: 'attack', weapon: 'missing' });
run('unit lacks healing', base(), { ability: 'heal' });
run('unit lacks capture', base('c01.knight'), { ability: 'capture' });
for (const ability of ['attack', 'heal', 'capture', 'wait']) run(`${ability} rejects unoffered weapon`, base('cleric'), { ability, weapon: 'missing' });
run('attack missing target with no eligible target', base(), { ability: 'attack' });
run('attack wrong target', base(), { ability: 'attack', target: { x: 5, y: 5 } });
run('attack target outside map', base(), { ability: 'attack', target: { x: -1, y: 1 } });
run('attack empty weapon is unusable', base(), { ability: 'attack', weapon: '' });
run('attack cooling before target requirement', base(), { ability: 'attack' }, s => s.units[0].weaponState['c01.border-blade'].cooldownRemaining = 1);
run('attack empty ammo', base('archer'), { ability: 'attack' }, s => { for (const account of Object.values(s.units[0].weaponState.archer_bow.resources)) account.current = 0; });
run('attack status blocked', base('mage'), { ability: 'attack' }, s => s.units[0].statuses.push({ id: 'silenced', remaining: 2, stacks: 1 }));
run('attack cannot move then fire', base('ballista'), { ability: 'attack' }, () => {}, false, [{ x: 1, y: 1 }, { x: 1, y: 2 }]);
run('attack allied target', base(), { ability: 'attack', target: { x: 2, y: 1 } }, s => spawnUnit(engine.rules, s, 'c01.swordsman', 1, { x: 2, y: 1 }));
run('heal missing target without wounded allies', base('cleric'), { ability: 'heal' });
run('heal illegal target', base('cleric'), { ability: 'heal', target: { x: 2, y: 1 } });
run('heal healthy ally', base('cleric'), { ability: 'heal', target: { x: 2, y: 1 } }, s => spawnUnit(engine.rules, s, 'c01.swordsman', 1, { x: 2, y: 1 }));
run('heal wounded ally', base('cleric'), { ability: 'heal', target: { x: 2, y: 1 } }, s => spawnUnit(engine.rules, s, 'c01.swordsman', 1, { x: 2, y: 1 }, { hp: 40 }), true);
run('capture noncapturable tile', base(), { ability: 'capture' });
run('capture status blocked', base(), { ability: 'capture' }, s => { s.map.tiles[8] = 'village'; s.units[0].statuses.push({ id: 'shaken', remaining: 2, stacks: 1 }); });
run('capture owned tile', base(), { ability: 'capture' }, s => { s.map.tiles[8] = 'village'; s.map.owners[8] = 1; });
run('capture neutral site', base(), { ability: 'capture' }, s => { s.map.tiles[8] = 'village'; }, true);
run('wait', base(), { ability: 'wait' }, () => {}, true);
run('wait empty weapon follows source truthiness', base(), { ability: 'wait', weapon: '' }, () => {}, true);
for (const resource of ['funds', 'command_points', 'momentum', 'weapon_uses']) for (const current of [null, 0, 10]) for (const requested of [-1, 0, 0.5, 10, 11, 'NaN', 'Infinity', '-Infinity']) {
  const holder = { id: 1, resources: { [resource]: { current, capacity: 100 } }, weaponState: { w: { resources: { [resource]: { current, capacity: 100 } } } } };
  const kind = resource === 'weapon_uses' ? 'weapon' : resource === 'momentum' ? 'unit' : 'player';
  const subject = kind === 'weapon' ? weaponResource(holder, 'w') : kind === 'unit' ? unitResource(holder) : playerResource(holder);
  let result = null, error = null;
  try { result = engine.rules.resources.canSpend(resource, subject, Number(requested)); } catch (caught) { if (caught.name !== 'DomainInvariantError') throw caught; error = { kind: 'DomainInvariant', message: caught.message }; }
  resources.push({ resource, kind, holder, requested, result, error });
}
const announcements = [];
for (const resource of ['funds', 'command_points', 'momentum', 'weapon_uses', 'missing']) for (const kind of ['player', 'unit', 'weapon']) for (const current of [10, null]) for (const amount of [0, 1]) {
  const holder = { id: 1, resources: { [resource]: { current, capacity: 100 } }, weaponState: { w: { resources: { [resource]: { current, capacity: 100 } } } } };
  const subject = kind === 'weapon' ? weaponResource(holder, 'w') : kind === 'unit' ? unitResource(holder) : playerResource(holder);
  let error = null; const events = [];
  try { engine.rules.resources.announce(subject, resource, amount, e => events.push(e)); }
  catch (caught) { if (caught.name !== 'DomainInvariantError') throw caught; error = { kind: 'DomainInvariant', message: caught.message }; }
  announcements.push({ resource, kind, holder, amount, events, error });
}
for (const amount of [0, 1]) {
  const holder = { id: 1, resources: {} }; let error = null; const events = [];
  try { engine.rules.resources.announce(playerResource(holder), 'funds', amount, e => events.push(e)); }
  catch (caught) { if (caught.name !== 'DomainInvariantError') throw caught; error = { kind: 'DomainInvariant', message: caught.message }; }
  announcements.push({ resource: 'funds', kind: 'player', holder, amount, events, error });
}
writeFileSync(process.env.EMPIRE_ACTION_ORACLE_OUTPUT, JSON.stringify({ packs, cases, resources, announcements }));
console.log(`Generated ${cases.length} action outcomes and ${resources.length} resource queries and ${announcements.length} announcements`);
