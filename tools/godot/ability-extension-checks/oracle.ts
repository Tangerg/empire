import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { Abilities, defineAbility } from '../../../packages/battle-engine/src/abilities';
import { CoreActionHandlers, commandOptions } from '../../../packages/battle-engine/src/actions';
import { ActionExecutionContext } from '../../../packages/battle-engine/src/action-system';
import { unitWeapons } from '../../../packages/battle-engine/src/combat';
import { resolveDamage } from '../../../packages/battle-engine/src/damage';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { DomainInvariantError } from '../../../packages/battle-engine/src/domain/errors';

// Offline source oracle for command semantics only, not a second engine composition path.
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
packs[packs.length - 1].statuses.push({ ...cloneStatus(), id: 'test-attack-seal', blockedAbilityTags: ['attack'] });
function cloneStatus() { return structuredClone(packs.flatMap((pack: any) => pack.statuses ?? []).find((status: any) => status.id === 'silenced')); }
const original = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
const clone = <T>(value: T): T => structuredClone(value);
const definition = (family: string, id: string) => packs.flatMap((pack: any) => pack[family] ?? []).find((entry: any) => entry.id === id);
function unit(id = 1, owner = 1, x = 2, y = 3, type = 'soldier', patch: any = {}): any {
  const def = definition('units', type);
  return { ...clone(original.units[1]), id, key: `ability-${id}`, owner, x, y, type, hp: def.maxHp,
    done: false, commanderId: null, formation: null, reaction: def.defaultReaction, learnedAbilities: [],
    resources: clone(def.resources), statuses: [],
    weaponState: Object.fromEntries(def.weapons.map((id: string) => [id, { cooldownRemaining: 0, resources: clone(definition('weapons', id).resources) }])), ...patch };
}
function base(units = [unit(), unit(2, 2, 3, 3)]): any {
  const state = clone(original);
  state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0),
    captureProgress: Array(49).fill(0), elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
  state.units = units; state.structures = []; state.composites = []; state.commanders = [];
  state.pendingCasts = []; state.embarkedUnits = []; state.markers = []; state.nextMarkerId = 1;
  state.scenario.engagementRules = []; state.scenario.zones = {};
  state.scenario.variables = {}; state.scenario.eventCounts = {}; state.rules.moraleEnabled = false;
  return state;
}
function abilities(config: any = {}) {
  const registry = Abilities.clone();
  registry.register(defineAbility({ id: 'test-charge', name: '蓄能', hint: '把一件武器充能到下一击。', priority: 12.5,
    weaponFor: (rules, query) => query.weaponId ? rules.content.weapons.get(query.weaponId) : null,
    weaponChoices: (rules, query) => config.duplicateChoices
      ? Array.from({ length: 40 }, (_, index) => ({ ...unitWeapons(rules.content, query.unit)[0], name: `duplicate-${index}` }))
      : unitWeapons(rules.content, query.unit),
    execute: (_rules, query, _target, emit) => {
      query.state.scenario.variables.charged = query.weaponId ?? '';
      query.state.scenario.variables.origin = `${query.at.x},${query.at.y}:${query.moved}`;
      emit({ type: 'scenarioSignal', signal: `charge:${query.weaponId ?? ''}` });
    },
  }));
  registry.register(defineAbility({ id: 'test-pulse', name: '脉冲', hint: '自定义效果', priority: 12.25, tags: ['command'],
    usable: (_rules, query) => query.state.scenario.variables.disabled !== true,
    execute: (_rules, query, _target, emit) => {
      query.state.scenario.variables.pulses = Number(query.state.scenario.variables.pulses ?? 0) + 1;
      emit({ type: 'scenarioSignal', signal: 'pulse' });
      if (query.state.scenario.variables.fail === true) throw new DomainInvariantError('custom ability failed');
    },
  }));
  registry.register(defineAbility({ id: 'test-mark', name: '标记', selfTargeted: false, priority: 12.25,
    engagement: config.engagement ?? 'hostile-action', targets: () => [{ x: 3, y: 3 }],
    execute: (_rules, query, target, emit) => {
      query.state.scenario.variables.marked = `${target!.x},${target!.y}`;
      emit({ type: 'scenarioSignal', signal: 'mark' });
    },
  }));
  registry.register(defineAbility({ id: 'test-replace', name: '替身', execute: (_rules, query, _target, emit) => {
    const index = query.state.units.findIndex(candidate => candidate.id === query.unit.id);
    query.state.units[index] = { ...query.unit, done: false };
    emit({ type: 'scenarioSignal', signal: 'actor-replaced' });
  } }));
  registry.register(defineAbility({ id: 'test-sacrifice', name: '献身', execute: (rules, query, _target, emit) => {
    resolveDamage(rules, query.state, { unit: query.unit.id, amount: query.unit.hp,
      report: blow => ({ type: 'statusTick', unit: query.unit.id, status: 'sacrifice', amount: blow.amount, hpAfter: blow.hpAfter }) }, emit);
  } }));
  for (const [id, priority] of [['test-😀', 12.25], ['test-\ue000', 12.25], ['test-low', 12.1]] as const)
    registry.register(defineAbility({ id, name: id, priority }));
  if (config.attackTags) registry.replace({ ...registry.get('attack'), tags: config.attackTags });
  if (config.markSelf) registry.replace({ ...registry.get('test-mark'), selfTargeted: true });
  return registry;
}
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const cases: any[] = [];
function run(name: string, state: any, command: any = null, config: any = {}, at?: any) {
  const initial = clone(state); state = clone(state);
  const engine = createBattleEngine({ content, abilities: abilities(config) });
  const actor = state.units[0], location = at ?? { x: actor.x, y: actor.y };
  const menus = commandOptions(engine.rules, state, actor, location);
  let events: any[] = [], error: any = null;
  if (command) {
    const context = new ActionExecutionContext(state, engine.rules);
    try {
      CoreActionHandlers.dispatch(context, command);
      events = context.events;
    } catch (caught) {
      const fault = caught as Error;
      if (!['DomainInvariantError', 'IllegalActionError'].includes(fault.name)) throw fault;
      error = { kind: fault.name.replace('Error', ''), message: fault.message };
      // The real engine owns rollback. Use its dispatch to establish error/rollback evidence.
      state = clone(initial);
      try { engine.dispatch(state, command); throw new Error('expected engine rejection'); }
      catch (failure) { if ((failure as Error).message !== fault.message) throw failure; }
    }
  }
  cases.push({ name, initial, command, config, at: location, menus, events, error, after: state, hash: hashState(state) });
}
function order(ability: string, extra: any = {}, path: any[] = [{ x: 2, y: 3 }]): any {
  return { kind: 'command', unit: 1, path, command: { ability, ...extra } };
}
for (const type of ['soldier', 'mage', 'c01.swordsman', 'c01.laiya']) {
  const state = base([unit(1, 1, 2, 3, type), unit(2, 2, 3, 3)]);
  run(`default ${type} stationary menu`, state);
  run(`default ${type} moved menu`, state, null, {}, { x: 2, y: 2 });
}
for (const type of ['soldier', 'mage']) {
  const state = base([unit(1, 1, 2, 3, type), unit(2, 2, 3, 3)]);
  state.units[0].learnedAbilities.push('test-charge', 'test-pulse', 'test-mark', 'test-😀', 'test-\ue000', 'test-low');
  run(`custom ${type} all menus`, state);
  for (const weapon of definition('units', type).weapons) run(`custom charge selected ${weapon}`, state, order('test-charge', { weapon }));
  run(`custom ${type} omitted weapon`, state, order('test-charge'));
  run(`custom ${type} unoffered weapon`, state, order('test-charge', { weapon: 'missing' }));
}
{
  const state = base([unit(1, 1, 2, 3, 'mage'), unit(2, 2, 3, 3)]);
  state.units[0].learnedAbilities.push('test-charge'); state.units[0].statuses.push({ id: 'silenced', remaining: 2, stacks: 1 });
  run('second weapon ability blocked by selected weapon tags', state, order('test-charge', { weapon: 'mage_meteor' }));
  run('weaponless order does not infer first weapon', state, order('test-charge'));
  state.units[0].statuses = []; state.units[0].weaponState.mage_bolt.cooldownRemaining = 3;
  run('custom choices need not require ready weapons', state, order('test-charge', { weapon: 'mage_bolt' }));
}
{
  const state = base(); state.units[0].statuses.push({ id: 'c01.oath-controlled', remaining: 2, stacks: 1 });
  run('replaced attack tags enter central status gate', state, order('attack', { target: { x: 3, y: 3 } }), { attackTags: ['command'] });
  state.units[0].learnedAbilities.push('test-pulse');
  run('custom ability tag blocks menu and execution', state, order('test-pulse'));
}
{
  const state = base(); state.units[0].statuses.push({ id: 'test-attack-seal', remaining: 2, stacks: 1 });
  run('default attack tag is blocked', state, order('attack', { target: { x: 3, y: 3 } }));
  run('replaced attack tags do not retain legacy hardcoded tag', state, order('attack', { target: { x: 3, y: 3 } }), { attackTags: [] });
  state.units[0].statuses = []; state.units[0].learnedAbilities.push('test-charge');
  run('stable duplicate weapon option keys', state, null, { duplicateChoices: true });
}
for (const mode of ['no-attacks', 'no-hostile-actions']) for (const engagement of ['attack', 'hostile-action']) for (const player of [1, 2]) {
  const state = base(); state.units[0].learnedAbilities.push('test-mark');
  state.scenario.zones.sanctuary = [{ x: 3, y: 3 }];
  state.scenario.engagementRules.push({ id: 'truce', zone: 'sanctuary', mode, players: [player] });
  run(`${mode} ${engagement} player ${player}`, state, order('test-mark', { target: { x: 3, y: 3 } }), { engagement });
}
{
  const state = base(); state.units[0].learnedAbilities.push('test-mark');
  run('custom targeted ability missing target', state, order('test-mark'));
  run('custom targeted ability forbidden target', state, order('test-mark', { target: { x: 4, y: 4 } }));
  run('self targeted ability accepts unrelated target', state, order('test-mark', { target: { x: 6, y: 6 } }), { markSelf: true });
  run('default wait rejects selected weapon', state, order('wait', { weapon: 'soldier_sword' }));
  run('default wait accepts optional facing target', state, order('wait', { target: { x: 2, y: 1 } }));
  run('ability not learned rejected', state, order('test-pulse'));
  run('unknown ability is invariant error', state, order('unknown'));
}
{
  const state = base(); state.units[0].learnedAbilities.push('test-pulse');
  run('custom side effect and event commit', state, order('test-pulse'));
  state.scenario.variables.disabled = true;
  run('custom usable denial', state, order('test-pulse'));
  state.scenario.variables.disabled = false; state.scenario.variables.fail = true;
  run('custom failure rolls back state and semantic events', state, order('test-pulse', {}, [{ x: 2, y: 3 }, { x: 2, y: 2 }]));
}
{
  const state = base(); state.units[0].learnedAbilities.push('test-charge');
  run('selected weapon preserved after canonical move', state, order('test-charge', { weapon: 'soldier_javelin' }, [{ x: 2, y: 3 }, { x: 2, y: 1 }]), {}, { x: 2, y: 1 });
  state.units[0].hp = 1; state.rules.zoneOfControl = true;
  run('death during movement suppresses custom effect', state, order('test-charge', { weapon: 'soldier_javelin' }, [{ x: 2, y: 3 }, { x: 1, y: 3 }]), {}, { x: 1, y: 3 });
}
{
  const state = base(); state.units[0].learnedAbilities.push('test-sacrifice');
  run('custom ability kills its own actor', state, order('test-sacrifice'));
}
{
  const state = base(); state.units[0].learnedAbilities.push('test-replace');
  run('custom ability replaces captured actor object', state, order('test-replace'));
}
for (const mode of ['instant', 'progressive']) {
  const state = base(); state.map.tiles[23] = 'village'; state.rules.captureMode = mode; state.units[0].hp = 40;
  run(`default capture ${mode}`, state, order('capture'));
}
{
  const state = base([unit(1, 1, 2, 3), unit(2, 1, 3, 3, 'soldier', { hp: 50 }), unit(3, 2, 5, 3)]);
  state.units[0].learnedAbilities.push('heal');
  run('default healing and canonical rank award', state, order('heal', { target: { x: 3, y: 3 } }));
}
{
  const state = base(); run('default attack', state, order('attack', { target: { x: 3, y: 3 }, weapon: 'soldier_sword' }));
  state.units[0].hp = 1; run('default counter kills actor', state, order('attack', { target: { x: 3, y: 3 } }));
}
{
  const state = base([unit(1, 1, 2, 3, 'mage'), unit(2, 2, 4, 3)]);
  run('default delayed cast', state, order('attack', { target: { x: 4, y: 3 }, weapon: 'mage_meteor' }));
}
const data = JSON.stringify({ packs, cases });
writeFileSync(process.argv[2], process.argv[2].endsWith('.gz') ? gzipSync(data) : data);
console.log(`Source ability command oracle: ${cases.length} cases`);
