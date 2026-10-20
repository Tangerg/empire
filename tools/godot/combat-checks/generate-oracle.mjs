import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { forecastCombatPlan, executeCombatPlan } from '../../../packages/battle-engine/src/combat-plan';
import { beginCast, resolveDueCasts } from '../../../packages/battle-engine/src/casting';
import { forceMoveUnit } from '../../../packages/battle-engine/src/forced-movement';
import { resolvePartingShots } from '../../../packages/battle-engine/src/zone-of-control';
import { damageStructure } from '../../../packages/battle-engine/src/structures';
import { weaponCoverage, WeaponAreaShapes } from '../../../packages/battle-engine/src/weapon-area';
import { abilityDef, abilityTargets, canUseAbility } from '../../../packages/battle-engine/src/abilities';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { addStatus, removeStatus, combinedStatusModifiers } from '../../../packages/battle-engine/src/statuses';
import { awardRankProgress } from '../../../packages/battle-engine/src/progression';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const output = process.env.EMPIRE_COMBAT_ORACLE_OUTPUT;
if (!output) throw new Error('Set EMPIRE_COMBAT_ORACLE_OUTPUT to the temporary oracle file.');
const packs = JSON.parse(readFileSync(`${root}/godot/data/content-packs.json`, 'utf8'));
const original = JSON.parse(readFileSync(`${root}/godot/tests/oracle/grayflag-initial-raw.json`, 'utf8')).state;
const definition = (family, id) => packs.flatMap(pack => pack[family] ?? []).find(entry => entry.id === id);
const clone = value => structuredClone(value);
function unit(id, owner, x, y, type = 'c01.swordsman', patch = {}) {
  const def = definition('units', type);
  return { ...clone(original.units[1]), id, key: `test-${id}`, owner, x, y, type,
    hp: def.maxHp, commanderId: null, formation: null, reaction: def.defaultReaction,
    resources: clone(def.resources), statuses: [],
    weaponState: Object.fromEntries(def.weapons.map(id => [id, { cooldownRemaining: 0, resources: clone(definition('weapons', id).resources) }])),
    ...patch };
}
function base(units = [unit(1, 1, 2, 3), unit(2, 2, 3, 3)]) {
  const state = clone(original);
  state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0),
    captureProgress: Array(49).fill(0), elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
  state.units = units; state.structures = []; state.composites = []; state.commanders = [];
  state.pendingCasts = []; state.embarkedUnits = []; state.markers = []; state.nextMarkerId = 1;
  state.scenario.engagementRules = []; state.scenario.terrainOverlays = []; state.scenario.zones = {};
  state.rules.moraleEnabled = false;
  return state;
}
const cases = [];
function run(name, state, op = 'attack', args = {}, patches = [], areas = []) {
  state = clone(state);
  const sourcePacks = clone(packs);
  for (const patch of patches) {
    Object.assign(sourcePacks.flatMap(pack => pack[patch.family] ?? []).find(entry => entry.id === patch.id), patch.values);
  }
  const content = createContentCatalog(); new ContentPackInstaller(content).install(...sourcePacks);
  const areaShapes = WeaponAreaShapes.clone();
  for (const area of areas) areaShapes.replace({
    id: area.id, needsOccupant: area.needsOccupant,
    cells: (_board, _from, target) => area.offsets.map(offset => ({ x: target.x + offset.x, y: target.y + offset.y })),
  });
  const rules = createBattleEngine({ content, areaShapes }).rules;
  const initial = clone(state), events = [], hashes = [];
  const emit = event => { events.push(event); hashes.push(hashState(state)); };
  const attacker = state.units.find(unit => unit.id === (args.unit ?? 1));
  const from = args.from ?? (attacker && { x: attacker.x, y: attacker.y });
  const target = args.target ?? { x: 3, y: 3 };
  const weapon = args.weapon ?? (attacker && content.units.get(attacker.type).weapons[0]);
  let result = null, error = null;
  try {
    if (op === 'attack' || op === 'forecast') {
      const plan = forecastCombatPlan(rules, state, attacker, target, { from, weapon });
      result = JSON.parse(JSON.stringify(plan));
      if (op === 'attack') executeCombatPlan(rules, state, plan, emit);
    } else if (op === 'query') {
      const ability = abilityDef(rules, 'attack');
      const query = { state, unit: attacker, at: from, moved: args.moved ?? false, weaponId: weapon };
      result = { usable: canUseAbility(rules, ability, query), targets: abilityTargets(rules, ability, query) };
    } else if (op === 'cast') {
      beginCast(state, { caster: attacker, ability: 'attack', weapon: content.weapons.get(weapon), target, origin: from }, emit);
    } else if (op === 'due') resolveDueCasts(rules, state, emit);
    else if (op === 'force') result = forceMoveUnit(rules, state, { unit: args.unit ?? 1, source: args.source, mode: args.mode, distance: args.distance, collisionDamage: args.collisionDamage }, emit);
    else if (op === 'parting') resolvePartingShots(rules, state, attacker, from, args.to, emit);
    else if (op === 'structure') result = damageStructure(content, state, args.id, args.raw === 'NaN' ? NaN : args.raw, emit);
    else if (op === 'coverage') result = weaponCoverage(rules, state, from, target, args.area);
    else if (op === 'status') {
      for (const step of args.steps) {
        if (step.remove) removeStatus(attacker, step.id, emit);
        else addStatus(content, attacker, { id: step.id, remaining: step.duration, sourceUnitId: step.source }, emit);
      }
      result = combinedStatusModifiers(content, attacker);
    } else if (op === 'progress') {
      for (const amount of args.amounts) awardRankProgress(rules, attacker, amount, emit);
    }
    else throw new Error(`Unknown operation ${op}`);
  } catch (caught) {
    if (!['DomainInvariantError', 'IllegalActionError'].includes(caught.name)) throw caught;
    error = { kind: caught.name.replace('Error', ''), message: caught.message };
  }
  cases.push({ name, initial, op, args, patches, areas, result, error, events, hashes, after: state });
}
const patchWeapon = values => [{ family: 'weapons', id: 'c01.border-blade', values }];
const structure = (id = 'gate', x = 3, y = 3, hp = 140, owner = 2) => ({ id, type: 'gate', owner, x, y, hp, disabled: false });
const pending = (caster = 1, weapon = 'mage_meteor', target = { x: 3, y: 3 }, declaredAt = 0, resolveAt = 2) =>
  ({ caster, owner: 1, ability: 'attack', weapon, target, origin: { x: 1, y: 3 }, declaredAt, resolveAt });
function charging() { const state = base([unit(1, 1, 1, 3, 'mage'), unit(2, 2, 3, 3)]); state.actorTurns = 2; state.pendingCasts = [pending()]; return state; }

for (const stance of ['counter', 'guard', 'support', 'conserve']) {
  for (const used of [false, true]) {
    const state = base(); state.units[1].reaction = stance; state.units[1].reactionUsedRound = used ? state.turn : -1;
    run(`${stance} reaction ${used ? 'spent' : 'ready'}`, state);
  }
}
for (const hp of [1, 50, 98, 150]) for (const rank of [0, 1, 2]) {
  const state = base(); Object.assign(state.units[0], { hp, rank, rankProgress: rank ? 319 : 119 });
  run(`strength hp=${hp} rank=${rank}`, state);
}
for (const facing of ['west', 'east', 'north', 'south']) {
  const state = base(); state.units[1].facing = facing;
  state.units.push(unit(3, 1, 4, 3)); run(`flanked defender faces ${facing}`, state);
}
for (const grid of ['square4', 'square8', 'hex']) {
  for (const area of ['single', 'cross1', 'ring1', 'line']) for (const target of [{ x: 3, y: 3 }, { x: 0, y: 0 }]) {
    const state = base(); state.rules.grid = grid;
    run(`${grid} ${area} coverage ${target.x}`, state, 'coverage', { area, target, from: { x: 1, y: 3 } });
  }
}
for (const area of ['cross1', 'ring1', 'line']) {
  const state = base([unit(1, 1, 1, 3), unit(2, 2, 3, 3), unit(3, 2, 4, 3), unit(4, 1, 3, 2), unit(5, 2, 2, 3)]);
  state.structures = [structure('primary', 3, 3), structure('splash', 3, 4), structure('allied', 2, 2, 140, 1)];
  run(`${area} mixed units and structures`, state, 'attack', {}, patchWeapon({ area }));
  const vacant = clone(state); vacant.units = vacant.units.filter(u => u.id !== 2); vacant.structures.shift();
  run(`${area} vacant primary splash`, vacant, 'attack', {}, patchWeapon({ area }));
}
{
  const state = base(); state.units.push(unit(3, 2, 3, 2, 'c01.swordsman', { reaction: 'support' }), unit(4, 2, 3, 4, 'c01.swordsman', { reaction: 'support' }));
  run('interception hp then id tie break', state);
  state.units[3].hp = 99; run('interception chooses higher hp', state);
  const support = base([unit(1, 1, 2, 3), unit(2, 2, 3, 3), unit(3, 1, 2, 2, 'archer', { reaction: 'support' }), unit(4, 1, 2, 4, 'archer', { reaction: 'support' })]);
  run('support fire equal damage tie break', support);
  support.units[1].hp = 1; run('primary kill suppresses support fire', support);
}
{
  const state = base([unit(1, 1, 2, 3, 'c01.laiya'), unit(2, 2, 3, 3)]);
  state.units[0].statuses = [{ id: 'inspired', remaining: 3, stacks: 2 }];
  state.units[1].statuses = [{ id: 'armor_down', remaining: 2, stacks: 2 }];
  state.map.elevation[23] = 2; run('rank status elevation gray oath riders', state, 'attack', { weapon: 'c01.gray-oath' });
}
for (const mode of ['push', 'pull']) for (const distance of [0, 0.49, 0.5, 2, 8]) {
  const state = base([unit(1, 1, 3, 3)]); state.units[0].capture = 17; state.map.captureProgress[24] = 17;
  run(`${mode} distance ${distance}`, state, 'force', { source: { x: 1, y: 3 }, mode, distance, collisionDamage: 10 });
}
{
  const state = base([unit(1, 1, 3, 3, 'c01.swordsman', { hp: 5 }), unit(2, 2, 4, 3)]);
  run('push collision kill', state, 'force', { source: { x: 2, y: 3 }, mode: 'push', distance: 2, collisionDamage: 10 });
  run('same source collision', base(), 'force', { source: { x: 2, y: 3 }, mode: 'push', distance: 1, collisionDamage: 10 });
  const cliff = base([unit(1, 1, 3, 3)]); cliff.map.elevation[25] = 9;
  run('push elevation blocked', cliff, 'force', { source: { x: 2, y: 3 }, mode: 'push', distance: 1, collisionDamage: 10 });
}
for (const stance of ['counter', 'guard', 'support', 'conserve']) {
  const state = base([unit(1, 1, 3, 3), unit(2, 2, 2, 3, 'rogue', { reaction: stance })]);
  state.rules.zoneOfControl = true;
  run(`parting ${stance} no rider or rank`, state, 'parting', { to: { x: 4, y: 3 } });
}
{
  const state = base([unit(1, 1, 3, 3, 'c01.swordsman', { hp: 1 }), unit(2, 2, 2, 3), unit(3, 2, 3, 2)]);
  state.rules.zoneOfControl = true;
  run('parting stops when mover dies', state, 'parting', { to: { x: 5, y: 3 } });
}
for (const [label, modify] of [
  ['resolves', () => {}], ['not due', s => s.actorTurns = 1],
  ['caster lost', s => s.units.shift()], ['weapon unavailable', s => s.units[0].weaponState.mage_meteor.cooldownRemaining = 1],
  ['target protected', s => { s.scenario.zones.safe = [{ x: 3, y: 3 }]; s.scenario.engagementRules = [{ id: 'truce', zone: 'safe', mode: 'no-attacks' }]; }],
  ['empty area resolves', s => { s.units[1].x = 3; s.units[1].y = 4; }],
  ['ally occupies primary', s => s.units[1].owner = 1],
  ['invalid duration', s => s.pendingCasts[0].resolveAt = 0],
]) { const state = charging(); modify(state); run(`cast ${label}`, state, 'due'); }
{
  const state = charging(); state.pendingCasts[0].weapon = 'mage_bolt'; state.units[1].x = 4;
  run('single cast vacated', state, 'due'); state.structures.push(structure()); run('single cast structure only vacated', state, 'due');
  const start = charging(); start.pendingCasts = []; run('cast declaration', start, 'cast', { weapon: 'mage_meteor' });
  const duplicate = charging(); run('duplicate cast refused', duplicate, 'cast', { weapon: 'mage_meteor' });
  const ordered = charging(); ordered.units.push(unit(3, 1, 1, 4, 'mage'));
  ordered.pendingCasts = [pending(3, 'mage_meteor', { x: 3, y: 3 }, 1, 2), pending(1, 'mage_meteor')];
  run('simultaneous casts oldest first', ordered, 'due');
}
for (const moved of [false, true]) for (const weapon of ['mage_bolt', 'mage_meteor']) {
  const state = charging(); state.pendingCasts = [];
  run(`query ${weapon} moved=${moved}`, state, 'query', { weapon, moved });
  state.units[0].statuses = [{ id: 'silenced', remaining: 2, stacks: 1 }];
  run(`query silenced ${weapon} moved=${moved}`, state, 'query', { weapon, moved });
}
for (const uses of [0, 1, 2, null]) {
  const state = base([unit(1, 1, 1, 3, 'soldier'), unit(2, 2, 3, 3)]);
  state.units[0].weaponState.soldier_javelin.resources.weapon_uses.current = uses;
  run(`limited weapon query uses=${uses}`, state, 'query', { weapon: 'soldier_javelin' });
  if (uses !== 0) run(`limited weapon spend uses=${uses}`, state, 'attack', { weapon: 'soldier_javelin' });
}
for (const hp of [0, 140]) for (const raw of [-1, 'NaN', 0, 1.5, 999]) {
  const state = base(); state.structures = [structure('gate', 3, 3, hp)];
  run(`structure hp=${hp} raw=${raw}`, state, 'structure', { id: 'gate', raw });
}
run('missing structure invalid raw precedence', base(), 'structure', { id: 'missing', raw: -1 });
run('forecast empty single is invariant', base([unit(1, 1, 2, 3)]), 'forecast');
run('forecast allied primary is invariant', base([unit(1, 1, 2, 3), unit(2, 1, 3, 3)]), 'forecast');
{
  const custom = [{ id: 'test.splash', needsOccupant: false, offsets: [{ x: 1, y: 0 }, { x: 0, y: 0 }, { x: 1, y: 0 }, { x: -99, y: 0 }] }];
  run('custom shape clipped deduplicated order', base(), 'coverage', { area: 'test.splash' }, [], custom);
  run('custom shape vacant primary resolves', base([unit(1, 1, 1, 3), unit(2, 2, 4, 3)]), 'attack', {},
    patchWeapon({ area: 'test.splash' }), custom);
  const cast = charging(); cast.units[1].x = 4;
  run('custom shape delayed vacant splash', cast, 'due', {},
    [{ family: 'weapons', id: 'mage_meteor', values: { area: 'test.splash' } }], custom);
  const replaceSingle = [{ id: 'single', needsOccupant: false, offsets: [{ x: 1, y: 0 }] }];
  run('replaced single no longer requires occupant', base([unit(1, 1, 1, 3), unit(2, 2, 4, 3)]), 'attack', {}, [], replaceSingle);
  const singleCast = charging(); singleCast.pendingCasts[0].weapon = 'mage_bolt'; singleCast.units[1].x = 4;
  run('replaced single cast no longer requires occupant', singleCast, 'due', {}, [], replaceSingle);
  const requireOccupant = [{ id: 'cross1', needsOccupant: true, offsets: [{ x: 1, y: 0 }] }];
  run('replaced cross requires occupant', base([unit(1, 1, 1, 3)]), 'forecast', {}, patchWeapon({ area: 'cross1' }), requireOccupant);
  run('replaced cross cast requires occupant', cast, 'due', {}, [], requireOccupant);
  run('unknown area fails loudly', base(), 'coverage', { area: 'test.missing' });
}
for (const weapon of packs.flatMap(pack => pack.weapons ?? [])) {
  const carrier = packs.flatMap(pack => pack.units ?? []).find(def => def.weapons.includes(weapon.id));
  if (!carrier) throw new Error(`No installed carrier exercises weapon ${weapon.id}`);
  for (const targetType of ['c01.swordsman', 'mage', 'knight']) {
    const distance = Math.max(1, weapon.minRange);
    const state = base([unit(1, 1, 1, 3, carrier.id), unit(2, 2, 1 + distance, 3, targetType)]);
    if ([...weapon.resourceRequirements, ...weapon.resourceCosts].some(cost => cost.resource === 'momentum'))
      state.units[0].resources.momentum = { current: 150, capacity: 150 };
    state.units[1].statuses = [{ id: 'c01.oath-controlled', remaining: 3, stacks: 1 }];
    run(`installed weapon ${weapon.id} against ${targetType}`, state, 'attack',
      { weapon: weapon.id, target: { x: 1 + distance, y: 3 } });
  }
}
for (const stackMode of ['refresh', 'extend', 'stack']) {
  run(`status ${stackMode} expiry and provenance`, base(), 'status',
    { steps: [
      { id: 'inspired', duration: 3, source: 2 }, { id: 'inspired', duration: 1 },
      { id: 'inspired', duration: 4, source: 3 }, { id: 'inspired', duration: 2, source: 4 },
      { id: 'shaken', duration: 2 }, { id: 'armor_down', duration: 3 },
    ] }, [{ family: 'statuses', id: 'inspired', values: { stackMode, maxStacks: 2 } }]);
}
run('status remove existing and absent', base(), 'status', { steps: [
  { id: 'inspired', duration: 2 }, { id: 'inspired', remove: true }, { id: 'inspired', remove: true },
] });
for (const career of packs.flatMap(pack => pack.careers ?? [])) {
  const state = base(); state.units[0].career = { current: career.id, unlocked: [career.id], mastery: { [career.id]: career.masteryThreshold - 1 } };
  run(`career mastery ${career.id}`, state, 'progress', { amounts: [0, -5, 0.49, 0.5, 120, 320] });
}
for (const amount of [0, 1, 2]) {
  const state = base(); state.players.find(player => player.id === 1).resources.funds.current = amount;
  const patches = patchWeapon({ resourceCosts: [
    { subject: 'player', resource: 'funds', amount: 0.5 }, { subject: 'player', resource: 'funds', amount: 0.5 },
  ] });
  run(`combined player costs readiness ${amount}`, state, 'query', {}, patches);
  run(`ordered fractional player costs execution ${amount}`, state, 'attack', {}, patches);
}
writeFileSync(output, JSON.stringify({ packs, cases }));
console.log(`Wrote ${cases.length} live TypeScript combat oracle cases`);
