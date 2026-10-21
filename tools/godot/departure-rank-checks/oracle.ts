import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { UnitDepartureHandlerRegistry, announceUnitDeparture } from '../../../packages/battle-engine/src/unit-departure';
import { CommanderDefeatDepartureHandler } from '../../../packages/battle-engine/src/commanders';
import { CastingCancellationDepartureHandler, beginCast, resolveDueCasts } from '../../../packages/battle-engine/src/casting';
import { awardRankProgress, awardCombatProgress, ThresholdRankProgressionPolicy } from '../../../packages/battle-engine/src/progression';
import { resolveDamage } from '../../../packages/battle-engine/src/damage';
import { changeMorale, surrenderUnit } from '../../../packages/battle-engine/src/morale';
import { applyScenarioEffect } from '../../../packages/battle-engine/src/scenario';
import { executeCombatPlan, forecastCombatPlan } from '../../../packages/battle-engine/src/combat-plan';
import { resolvePartingShots } from '../../../packages/battle-engine/src/zone-of-control';
import { StatusLifecycleContext } from '../../../packages/battle-engine/src/statuses';
import { forceMoveUnit } from '../../../packages/battle-engine/src/forced-movement';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { DomainInvariantError } from '../../../packages/battle-engine/src/domain/errors';
import { makeLevel, u } from '../../../packages/test-content/src/levels';
import type { GameState, GameEvent } from '../../../packages/battle-engine/src/types';
import type { UnitDepartureHandler } from '../../../packages/battle-engine/src/unit-departure';
import type { RankProgressionPolicy } from '../../../packages/battle-engine/src/progression';

const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const baseline = createBattleEngine({ content });
const clone = <T>(value: T): T => structuredClone(value);
const original = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
function base(): GameState {
  return baseline.createState(makeLevel(Array(7).fill('.......'), { units: [u(1, 3, 'soldier', 1),
    u(2, 3, 'soldier', 2), u(1, 4, 'mage', 1), u(5, 5, 'soldier', 2)], rules: { moraleEnabled: false } }));
}
function witness(id: string, label = id, fail = false): UnitDepartureHandler {
  return { id, handle: ({ rules, state, unit, emit }) => {
    const visits = (state.scenario.variables.visits ??= []) as any[];
    visits.push({ label, unit: unit.id, owner: unit.owner, hp: unit.hp,
      onBoard: state.units.some(candidate => candidate.id === unit.id), markers: state.markers.length,
      casts: state.pendingCasts.map(cast => cast.caster), handlers: rules.unitDepartures.keys(),
      name: rules.content.units.get(unit.type).name,
      distance: rules.grids.get(state.rules.grid).distance(unit, { x: 0, y: 0 }) });
    emit({ type: 'scenarioSignal', signal: `${label}:${unit.id}` });
    if (fail) throw new DomainInvariantError('custom departure failed');
  } };
}
function departures(kind: string) {
  let registry = new UnitDepartureHandlerRegistry();
  if (kind !== 'only' && kind !== 'empty') {
    const defaults = kind === 'reverse' ? [CastingCancellationDepartureHandler, CommanderDefeatDepartureHandler]
      : [CommanderDefeatDepartureHandler, CastingCancellationDepartureHandler];
    for (const handler of defaults) registry.register(handler);
  }
  if (kind === 'replaceCommander') registry.replace(witness('commander.defeat', 'commander.replacement'));
  if (kind === 'replaceCast') registry.replace(witness('casting.cancel', 'casting.replacement'));
  if (kind === 'snapshot') registry.register({ id: 'test.mutate-snapshot', handle: ({ unit, emit }) => { unit.hp = 77; emit({ type: 'scenarioSignal', signal: 'mutated-snapshot' }); } });
  if (kind !== 'default' && kind !== 'empty') registry.register(witness('test.first', 'first', kind === 'throw')).register(witness('test.second', 'second'));
  return registry;
}
function progression(config: any): RankProgressionPolicy {
  if (config.kind === 'custom') return {
    rankFor: progress => progress < 10 ? 0 : progress < 20 ? 2 : 1,
    nextThreshold: rank => rank === 0 ? 10 : null,
  };
  return new ThresholdRankProgressionPolicy(config.veteran ?? 120, config.elite ?? 320);
}
function commanderCast(state: GameState, nested = false) {
  state.commanders = [{ ...clone(original.commanders[0]), id: 'test', unitId: 1, owner: 1 }];
  state.units[2].commanderId = 'test';
  for (const id of nested ? [1, 3] : [1]) state.pendingCasts.push({ caster: id, owner: 1, ability: 'metadata',
    weapon: 'mage_meteor', target: { x: 2, y: 3 }, origin: { x: 1, y: 3 }, declaredAt: 0, resolveAt: 9 });
  if (nested) {
    state.rules.moraleEnabled = true; state.units[2].morale.current = 1;
    state.units[2].morale.resilience = 0;
  }
}
const cases: any[] = [];
function run(name: string, op: string, params: any = {}, config: any = {}, prepare: (state: GameState) => void = () => {}) {
  const state = base(); prepare(state); const initial = clone(state);
  const events: GameEvent[] = [], eventHashes: string[] = [];
  const emit = (event: GameEvent) => { events.push(event); eventHashes.push(hashState(state)); };
  let result: any = null, error: any = null, keys: string[] | null = null;
  try {
    const policy = progression(config.progression ?? {});
    const registry = departures(config.departures ?? 'default');
    const engine = createBattleEngine({ content, progression: policy, unitDepartures: registry });
    const rules = engine.rules; keys = registry.keys();
    const actor = state.units[0];
    switch (op) {
      case 'policy': result = { ranks: params.progress.map((value: number) => policy.rankFor(value)),
        thresholds: params.ranks.map((value: any) => policy.nextThreshold(value)) }; break;
      case 'rank': for (const amount of params.amounts) awardRankProgress(rules, actor, amount, emit); break;
      case 'combatProgress': awardCombatProgress(rules, actor, params.damage, params.defeated, emit); break;
      case 'damage': result = resolveDamage(rules, state, { unit: params.unit ?? 1, amount: params.amount ?? 999,
        nonlethal: params.nonlethal ?? false, report: blow => ({ type: 'statusTick', unit: params.unit ?? 1,
          status: 'poisoned', amount: blow.amount, hpAfter: blow.hpAfter }) }, emit); break;
      case 'parting': resolvePartingShots(rules, state, actor, { x: 1, y: 3 }, { x: 0, y: 3 }, emit); break;
      case 'status': result = new StatusLifecycleContext(rules, state, actor, { id: 'poisoned', remaining: 1, stacks: 1 }, emit).damage(999, false); break;
      case 'morale': result = changeMorale(rules, state, 1, -999, 'test', emit); break;
      case 'surrender': result = surrenderUnit(rules, state, 1, 2, emit); break;
      case 'withdraw': applyScenarioEffect(rules, state, { type: 'withdrawUnits', selector: { ids: [1] }, leaveCorpse: params.leaveCorpse ?? false }, emit); break;
      case 'announce': announceUnitDeparture(rules, state, actor, emit); break;
      case 'combat': executeCombatPlan(rules, state, forecastCombatPlan(rules, state, actor,
        { x: state.units[1].x, y: state.units[1].y }), emit); break;
      case 'ability': rules.abilities.get(params.ability).execute(rules, { state, unit: actor, at: { x: actor.x, y: actor.y }, moved: false },
        params.target ?? null, emit); break;
      case 'collision': result = forceMoveUnit(rules, state, { unit: 1, source: { x: 2, y: 3 }, mode: 'push', distance: 9, collisionDamage: 999 }, emit); break;
      case 'delayed':
        beginCast(state, { caster: state.units[2], ability: 'metadata', weapon: content.weapons.get('mage_meteor'),
          target: { x: 2, y: 3 }, origin: { x: 1, y: 4 } }, emit);
        state.actorTurns += 2; resolveDueCasts(rules, state, emit); break;
      default: throw new Error(`unknown op ${op}`);
    }
  } catch (caught) {
    const fault = caught as Error;
    if (!['DomainInvariantError', 'IllegalActionError'].includes(fault.name)) throw fault;
    error = { kind: fault.name.replace('Error', ''), message: fault.message };
  }
  cases.push({ name, op, params, config, initial, keys, result, error, events, eventHashes, after: state, hash: hashState(state) });
}
for (const config of [{}, { veteran: 10, elite: 30 }, { veteran: 10.5, elite: 30.5 }, { kind: 'custom' }]) {
  run(`policy ${JSON.stringify(config)}`, 'policy', { progress: [-1, 0, 9.99, 10, 10.5, 19, 20, 30, 30.5, 120, 320], ranks: [-1, 0, 1, 2, 3] }, { progression: config });
  run(`rank sequence ${JSON.stringify(config)}`, 'rank', { amounts: [-1, -0.5, 0, 0.49, 0.5, 1.5, 9.5, 19.5, 119.5, 319.5] }, { progression: config });
}
for (const [veteran, elite] of [[0, 3], [-1, 3], [3, 3], [4, 3]])
  run(`invalid thresholds ${veteran},${elite}`, 'policy', { progress: [], ranks: [] }, { progression: { veteran, elite } });
run('custom policy never demotes existing rank', 'rank', { amounts: [10, 10] }, { progression: { kind: 'custom' } });
for (const damage of [-3, 0, 0.5, 11.5]) for (const defeated of [false, true])
  run(`custom combat progress ${damage} defeated ${defeated}`, 'combatProgress', { damage, defeated }, { progression: { veteran: 10, elite: 30 } });
for (const ability of ['capture', 'heal']) run(`custom progression ${ability} consumer`, 'ability', { ability,
  target: ability === 'heal' ? { x: 1, y: 4 } : null }, { progression: { veteran: 10, elite: 30 } }, state => {
    state.map.tiles[22] = 'village'; state.units[2].hp = 20;
  });
run('custom progression combat consumer', 'combat', {}, { progression: { veteran: 1, elite: 2 } });
run('custom progression support interception consumer', 'combat', {}, { progression: { veteran: 1, elite: 2 } }, state => {
  state.units[2].owner = 2; state.units[2].x = 2; state.units[2].y = 4; state.units[2].reaction = 'support';
});
for (const mode of ['default', 'append', 'replaceCommander', 'replaceCast', 'reverse', 'only', 'empty', 'throw'])
  run(`departure ${mode} commander nested`, 'damage', {}, { departures: mode }, state => commanderCast(state, true));
for (const op of ['damage', 'morale', 'surrender', 'withdraw', 'collision', 'announce'])
  run(`custom departure ${op} route`, op, {}, { departures: 'append' }, state => {
    commanderCast(state); state.rules.moraleEnabled = op === 'morale';
  });
run('nonlethal does not announce departure', 'damage', { nonlethal: true }, { departures: 'append' });
run('missing target does not announce departure', 'damage', { unit: 999 }, { departures: 'append' });
run('transport passengers are markers not extra departure announcements', 'damage', {}, { departures: 'append' }, state => {
  commanderCast(state); state.embarkedUnits = [4, 5].map(id => ({ carrier: 1, unit: { ...clone(state.units[2]), id } }));
});
run('custom departure combat kill route', 'combat', {}, { departures: 'append', progression: { veteran: 1, elite: 2 } }, state => { state.units[1].hp = 1; });
run('custom departure counter kill route', 'combat', {}, { departures: 'append', progression: { veteran: 1, elite: 2 } }, state => { state.units[0].hp = 1; });
run('custom departure delayed combat route', 'delayed', {}, { departures: 'append', progression: { veteran: 1, elite: 2 } }, state => { state.units[1].hp = 1; });
run('custom departure parting shot route', 'parting', {}, { departures: 'append' }, state => { state.units[0].hp = 1; state.rules.zoneOfControl = true; });
run('custom departure status damage route', 'status', {}, { departures: 'append' });
run('departure snapshot shared by handlers but detached from marker', 'damage', {}, { departures: 'snapshot' });
const registry = new UnitDepartureHandlerRegistry().register(CommanderDefeatDepartureHandler).register(CastingCancellationDepartureHandler);
let duplicateError = '', sealedError = '', missingError = '';
try { registry.register(CommanderDefeatDepartureHandler); } catch (error) { duplicateError = (error as Error).message; }
const copy = registry.clone();
copy.replace(witness('commander.defeat', 'changed')).replace(witness('test.new'));
const shallowClone = registry.clone().get('casting.cancel') === registry.get('casting.cancel');
registry.seal();
try { registry.register(witness('test.blocked')); } catch (error) { sealedError = (error as Error).message; }
try { registry.get('missing'); } catch (error) { missingError = (error as Error).message; }
const registryFacts = { duplicateError, sealedError, missingError, originalKeys: registry.keys(), copyKeys: copy.keys(), shallowClone };
for (const item of cases.filter(item => item.name.startsWith('custom departure ') && !item.name.includes('announce'))) {
  if (!item.events.some((event: any) => event.type === 'scenarioSignal'))
    throw new Error(`${item.name}: source fixture never reached a custom consequence`);
}
for (const item of cases.filter(item => item.name.startsWith('custom progression '))) {
  if (!item.events.some((event: any) => event.type === 'rankChanged'))
    throw new Error(`${item.name}: source fixture never used the injected rank policy`);
}
const data = JSON.stringify({ packs, cases, registryFacts });
writeFileSync(process.argv[2], process.argv[2].endsWith('.gz') ? gzipSync(data) : data);
console.log(`Source departure/rank oracle: ${cases.length} cases`);
