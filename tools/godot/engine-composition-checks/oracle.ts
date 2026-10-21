import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { defineAbility } from '../../../packages/battle-engine/src/abilities';
import { BattleRecorder, hashState, replayBattle } from '../../../packages/battle-engine/src/replay';
import { DomainInvariantError } from '../../../packages/battle-engine/src/domain/errors';
import { SplitMixRandom } from '../../../packages/battle-engine/src/random';
import { DefaultTacticalSpace } from '../../../packages/battle-engine/src/tactical-space';
import type { EnginePlugin } from '../../../packages/battle-engine/src/kernel';

const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const level = JSON.parse(readFileSync('godot/tests/oracle/grayflag-0.58.json', 'utf8')).level;
function content(sourcePacks = packs) { const value = createContentCatalog(); new ContentPackInstaller(value).install(...sourcePacks); return value; }
const clone = <T>(value: T): T => structuredClone(value);
function plugins(version: number, trace: string[]): EnginePlugin[] {
  return [{ id: 'test.root-extension', version,
    overrides: ['abilities', 'actionHandlers'], requiresCapabilities: ['abilities', 'actionHandlers', 'resources'],
    install(context) {
      trace.push('extension');
      const resources = context.require('resources');
      const abilities = context.require('abilities').clone().register(defineAbility({ id: 'root-pulse', name: '组合脉冲',
        priority: 12.5, hint: '使用组合资源', execute(_rules, query, _target, emit) {
          const amount = resources.spend('root_energy', { kind: 'unit', unit: query.unit }, 1.5);
          resources.announce({ kind: 'unit', unit: query.unit }, 'root_energy', -amount, emit);
          query.state.scenario.variables.pulses = Number(query.state.scenario.variables.pulses ?? 0) + 1;
          emit({ type: 'scenarioSignal', signal: 'root-pulse' });
          if (query.state.scenario.variables.fail) throw new DomainInvariantError('root pulse failed');
        } }));
      context.replace('abilities', abilities);
      const actions = context.require('actionHandlers').clone();
      actions.register({ kind: 'rootGrant', execute(context: any, action: any) {
        const unit = context.state.units.find((unit: any) => unit.id === 2);
        unit.learnedAbilities = ['root-pulse']; unit.resources.root_energy = { current: 4, capacity: 5 };
        unit.done = false; context.state.scenario.variables.fail = action.fail === true;
        context.emit({ type: 'scenarioSignal', signal: 'root-grant' });
      } } as any);
      context.replace('actionHandlers', actions);
    } },
    { id: 'test.root-resources', version: 1, overrides: ['resources'], requiresCapabilities: ['resources'], install(context) {
      trace.push('resources');
      const resources = context.require('resources').clone();
      resources.adapters.register({ id: 'root_energy', name: '组合能量', subjectKind: 'unit', integer: false, aiWeight: 0.25,
        account: (subject: any) => { const account = subject.unit.resources.root_energy; return account ? {
          read: () => ({ current: account.current, capacity: account.capacity }),
          write: (current: number) => { account.current = current; },
        } : null; } });
      context.replace('resources', resources);
    } },
    { id: 'test.root-ai', version: 1, overrides: ['aiIntents'], requiresCapabilities: ['aiIntents', 'abilities'], install(context) {
      trace.push('ai');
      context.replace('aiIntents', context.require('aiIntents').clone().register({ id: 'root-choice', priority: -1000,
        propose: () => ({ kind: 'rootGrant' } as any) }));
    } }];
}
function error(call: () => unknown): any {
  try { call(); return null; } catch (value) { const error = value as Error; return { name: error.name.replace('Error','Exception'), message: error.message }; }
}
const cases = [1, 2147483648, 1e21].map(version => {
  const trace: string[] = [], engine = createBattleEngine({ content: content(), plugins: plugins(version, trace) });
  const state = engine.createState(level); const initial = clone(state);
  const recorder = new BattleRecorder(engine, state);
  const grant = { kind: 'rootGrant' } as any;
  const grantEvents = engine.dispatch(state, grant); recorder.record(grant);
  const actor = state.units.find(unit => unit.id === 2)!;
  const options = engine.commandsAt(state, actor, actor).map(option => ({ ability: option.ability, key: option.key }));
  const pulse = { kind: 'command', unit: actor.id, path: [{ x: actor.x, y: actor.y }], command: { ability: 'root-pulse' } } as any;
  const pulseEvents = engine.dispatch(state, pulse); recorder.record(pulse);
  const save = engine.saveBattle(state, '2026-10-20T00:00:00.000Z');
  const replay = recorder.replay(state);
  const replayed = replayBattle(engine, level, replay);
  const final = clone(state);
  const failGrant = { kind: 'rootGrant', fail: true } as any;
  engine.dispatch(state, failGrant); const beforeFailure = clone(state);
  const failure = error(() => engine.dispatch(state, pulse));
  const other = createBattleEngine({ content: content(), plugins: plugins(version + 1, []) });
  return { version, trace, manifest: engine.rulesetManifest, initial, grant, grantEvents, pulse, pulseEvents, options,
    final, save, loaded: engine.loadBattle(save), replay, replayed: replayed.state, replayDivergedAt: replayed.divergedAt,
    ai: engine.chooseAiAction(initial), failGrant, beforeFailure, failure, afterFailure: state,
    wrongVersion: version === 1e21 ? null : error(() => other.loadBattle(save)),
    hash: hashState(final), seals: {
      abilities: error(()=>engine.rules.abilities.register(defineAbility({ id:'late',name:'late' }))),
      actions: error(()=>engine.actionHandlers.register({kind:'late',execute:()=>{}} as any)),
      resources: error(()=>engine.rules.resources.adapters.register({id:'late',name:'late',subjectKind:'unit',integer:true,aiWeight:1,account:()=>null})),
      ai: error(()=>engine.aiIntents.register({id:'late',priority:1,propose:()=>null})),
    } };
});
const defaultEngine = createBattleEngine({ content: content() });
const anonymous = createBattleEngine({ content: content(), resources: defaultEngine.rules.resources.clone() });
const randomCases = [false, true].map(failRandom => {
  const randomLevel = clone(level);
  randomLevel.scenario.triggers = [{ id: 'root-random', timing: 'afterAction',
    condition: { type: 'chance', percent: 50, stream: 'root.chance' }, effects: [{ type: 'emitSignal', signal: 'random-fired' }] }];
  randomLevel.scenario.variables = { failRandom };
  const randomPlugin: EnginePlugin = { id: 'test.root-random', version: 1, overrides: ['random'], install(context) {
    context.replace('random', { ...SplitMixRandom, id: 'root-random', chance(state, stream, percent) {
      state.random.seed = (state.random.seed + 17) >>> 0;
      state.random.counters[stream] = (state.random.counters[stream] ?? 0) + 3;
      state.random.counters['other-stream'] = 7;
      state.scenario.variables.randomSeen = percent;
      if (state.scenario.variables.failRandom) throw new DomainInvariantError('random policy failed');
      return true;
    } });
  } };
  const engine = createBattleEngine({ content: content(), plugins: [...plugins(1, []), randomPlugin] });
  const state = engine.createState(randomLevel); const before = clone(state);
  const recorder = new BattleRecorder(engine,state); const action = { kind: 'rootGrant' } as any;
  let events: unknown = null;
  const failure = error(() => { events = engine.dispatch(state, action); recorder.record(action); });
  const save = engine.saveBattle(state, '2026-10-20T00:00:00.000Z');
  const replay = recorder.replay(state);
  return { failRandom, level: randomLevel, before, action, events, failure, state, save, replay,
    loaded: engine.loadBattle(save), replayed: replayBattle(engine, randomLevel, replay).state };
});
class PeaceSpace extends DefaultTacticalSpace { override attackTargets(): [] { return []; } }
const spacePlugin: EnginePlugin = { id: 'test.root-space', version: 1, overrides: ['space'], requiresCapabilities: ['content', 'grids'],
  install: context => context.replace('space', new PeaceSpace(context.require('content'),context.require('grids'))) };
const peace = createBattleEngine({ content: content(), plugins: [spacePlugin] });
const spaceInput = defaultEngine.createState(level);
const actor = spaceInput.units.find(unit => unit.id === 2)!;
const target = spaceInput.units.find(unit => unit.owner === 2)!;
target.x = actor.x + 1; target.y = actor.y; spaceInput.units = [actor, target]; spaceInput.commanders = []; spaceInput.composites = [];
const attack = { kind: 'command', unit: actor.id, path: [{ x: actor.x, y: actor.y }], command: { ability: 'attack', target: { x: target.x, y: target.y } } } as any;
const spaceState = clone(spaceInput);
const spatial = { input: spaceInput, attack, defaultOptions: defaultEngine.commandsAt(spaceInput,actor,actor).map(option => option.key),
  customOptions: peace.commandsAt(spaceInput,actor,actor).map(option => option.key), error: error(() => peace.dispatch(spaceState,attack)), after: spaceState };
const combatPacks = clone(packs);
combatPacks.flatMap((pack: any) => pack.weapons ?? []).find((weapon: any) => weapon.id === 'c01.border-blade').hitEffects = [{ type: 'rootMark' }];
const combatContent = content(combatPacks);
const combatPlugin: EnginePlugin = { id: 'test.root-combat', version: 1,
  overrides: ['combatModifiers','hitEffects'], requiresCapabilities: ['content','combatModifiers','hitEffects'], install(context) {
    const catalog = context.require('content');
    context.replace('combatModifiers', context.require('combatModifiers').register({ id: 'root-bonus', priority: 999,
      provide(query) {
        if(query.content !== catalog) throw new Error('modifier catalog identity');
        return [{ id: 'root-bonus', label: 'root bonus', source: 'root', stage: 'final', operation: 'add', value: 7 }];
      } }));
    context.replace('hitEffects', context.require('hitEffects').clone().register({ kind: 'rootMark',
      apply(query: any) {
        if(query.content !== catalog) throw new Error('hit-effect catalog identity');
        query.state.scenario.variables.rootHit = query.target.id;
        query.emit({ type: 'scenarioSignal', signal: 'root-hit' });
      }, describe: () => 'root mark' } as any));
  } };
const combatEngine = createBattleEngine({ content: combatContent, plugins: [combatPlugin] });
const combatState = clone(spaceInput);
const forecast = combatEngine.forecast(combatState,combatState.units[0],combatState.units[1]);
const combatEvents = combatEngine.dispatch(combatState,attack);
const combat = { packs: combatPacks, input: spaceInput, action: attack, forecast, events: combatEvents, state: combatState,
  manifest: combatEngine.rulesetManifest, save: combatEngine.saveBattle(combatState,'2026-10-20T00:00:00.000Z') };
const openTrace: string[] = [];
const openEngine = createBattleEngine({ content: content(), plugins: [
  { id: 'test.open-consumer', version: 1, requiresCapabilities: ['mod.root.flag'] as any,
    install: context => { openTrace.push(`consumer:${context.require('mod.root.flag' as any)}`); } },
  { id: 'test.open-override', version: 1, overrides: ['mod.root.flag'] as any,
    install: context => { openTrace.push('override'); context.replace('mod.root.flag' as any,'replacement' as any); } },
  { id: 'test.open-provider', version: 1, provides: ['mod.root.flag'] as any,
    install: context => { openTrace.push('provider'); context.provide('mod.root.flag' as any,'original' as any); } },
] });
let displacedGrids: any;
const gridCatalog=content();
const gridEngine=createBattleEngine({content:gridCatalog,plugins:[{id:'test.grid-swap',version:1,overrides:['grids'],requiresCapabilities:['grids'],install(context){
  displacedGrids=context.require('grids'); context.replace('grids',displacedGrids.clone());
}}]});
const capture={sameContent:(gridEngine.space as DefaultTacticalSpace).content===gridCatalog,
  capturedOriginal:(gridEngine.space as DefaultTacticalSpace).grids===displacedGrids,
  resolvedDifferent:gridEngine.rules.grids!==displacedGrids,
  originalMutation:error(()=>displacedGrids.replace(displacedGrids.get('square4'))),
  finalMutation:error(()=>gridEngine.rules.grids.replace(gridEngine.rules.grids.get('square4')))};
const result = { level, cases, randomCases, spatial, combat, openTrace, openManifest:openEngine.rulesetManifest, capture, defaultManifest: defaultEngine.rulesetManifest,
  anonymousManifest: anonymous.rulesetManifest,
  anonymousSave: error(() => anonymous.saveBattle(anonymous.createState(level))),
  anonymousRecord: error(() => new BattleRecorder(anonymous, anonymous.createState(level))) };
writeFileSync(process.argv[2], gzipSync(JSON.stringify(result)));
console.log(`Source engine composition oracle: ${cases.length} named versioned custom engines`);
