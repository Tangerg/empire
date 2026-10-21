import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { TurnOrders, activeTurnOrder, mayAct, type TurnOrderPolicy } from '../../../packages/battle-engine/src/turn-order';
import { Reactions, type ReactionBehavior } from '../../../packages/battle-engine/src/reactions';
import { BattleLifecycle } from '../../../packages/battle-engine/src/turn-cycle';
import { forecast } from '../../../packages/battle-engine/src/combat';
import { forecastCombatPlan, executeCombatPlan } from '../../../packages/battle-engine/src/combat-plan';
import { hashState } from '../../../packages/battle-engine/src/replay';
import type { Action, GameEvent, GameState } from '../../../packages/battle-engine/src/types';

type Data = Record<string, any>;
const clone = <T>(value: T): T => structuredClone(value);
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const original = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const defaults = createBattleEngine({ content });
function stateFor(): GameState {
  const state = clone(original);
  state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0), captureProgress: Array(49).fill(0), elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
  state.units = [[1, 1, 2, 3], [2, 2, 3, 3], [3, 2, 3, 4]].map(([id, owner, x, y]) => {
    const definition = content.units.get('soldier');
    return { ...clone(original.units[0]), id, owner, x, y, key: `policy-${id}`, type: 'soldier', hp: definition.maxHp,
      done: false, formation: null, commanderId: null, reaction: 'counter', statuses: [], learnedAbilities: [],
      resources: clone(definition.resources), weaponState: Object.fromEntries(definition.weapons.map(weapon => [weapon, { cooldownRemaining: 0, resources: clone(content.weapons.get(weapon).resources) }])) };
  });
  state.commanders = []; state.structures = []; state.composites = []; state.pendingCasts = []; state.embarkedUnits = []; state.markers = [];
  state.scenario = { variables: {}, zones: {}, overlays: [], triggers: [], firedTriggerIds: [], triggerRuntime: {}, eventCounts: {}, zoneTags: {}, engagementRules: [] };
  for (const player of state.players) { player.objectives = [{ id: 'route', type: 'routEnemies', label: '击退敌军' }]; player.objectiveStates = { route: { id: 'route', status: 'active', hidden: false } }; }
  return state;
}
function policy(config: Data = {}): TurnOrderPolicy {
  const id = config.id ?? 'reverse';
  return {
    id, name: '倒序自定义',
    initialState: (state, catalog) => ({ policy: config.switchToSide ? 'side' : id, activeUnit: null, data: { step: 0, movement: catalog.units.get(state.units[0].type).movement } }),
    begin: (state, context) => {
      context.emit({ type: 'scenarioSignal', signal: `${id}.begin.${state.turnOrder.policy}` });
      const actor = state.units[state.units.length - 1];
      state.turnOrder.data.step = 1;
      return { player: actor.owner, activeUnit: actor.id, roundAdvanced: false };
    },
    advance: (state, context) => {
      const step = ++state.turnOrder.data.step;
      context.emit({ type: 'scenarioSignal', signal: `${id}.advance.${step}` });
      if (config.exhaust && step >= 3) return { player: state.currentPlayer, activeUnit: null, roundAdvanced: true, exhausted: true };
      const actor = state.units[(state.units.length - step % state.units.length) % state.units.length];
      return { player: actor.owner, activeUnit: actor.id, roundAdvanced: step % 2 === 0 };
    },
    canAct: (state, unit) => state.turnOrder.activeUnit === unit.id && !unit.done,
    actors: state => config.independent ? [state.units[1], state.units[0]] : state.units.filter(unit => state.turnOrder.activeUnit === unit.id && !unit.done),
    preview: (state, catalog, count) => config.independent ? [catalog.units.get(state.units[0].type).movement, count, state.units[0].id]
      : Array.from({ length: Math.max(0, count) }, (_, index) => state.units[(state.units.length - (state.turnOrder.data.step + index) % state.units.length) % state.units.length].id),
  };
}
const turns: Data[] = [];
function turnCase(name: string, state: GameState, operation: string, config: Data = {}): void {
  const registry = TurnOrders.clone(); if (config.custom) registry.define(policy(config));
  const rules = { ...defaults.rules, turnOrders: registry }, before = clone(state), events: GameEvent[] = [], hashes: string[] = [];
  const emit = (event: GameEvent) => { events.push(event); state.scenario.eventCounts[event.type] = (state.scenario.eventCounts[event.type] ?? 0) + 1; hashes.push(hashState(state)); };
  const lifecycle = new BattleLifecycle(state, rules, emit);
  if (operation === 'start') lifecycle.start();
  else if (operation === 'beginPlaying') lifecycle.beginPlaying();
  else if (operation === 'advance') lifecycle.advanceTurn();
  const selected = activeTurnOrder(rules, state), beforeQueries = hashState(state);
  const query = { active: selected.id, name: selected.name, canAct: state.units.map(unit => selected.canAct(state, unit)), mayAct: state.units.map(unit => mayAct(rules, state, unit)), actors: selected.actors(state).map(unit => unit.id), preview: selected.preview(state, content, config.count ?? 5) };
  if (hashState(state) !== beforeQueries) throw new Error('Source policy query mutated state');
  turns.push({ name, config, operation, before, query, events, hashes, after: clone(state), hash: hashState(state) });
}
for (const id of ['side', 'initiative', 'reverse']) {
  const state = stateFor(); state.rules.turnOrder = id;
  const config = { custom: id === 'reverse' };
  turnCase(`${id} start`, state, 'start', config);
  for (let i = 0; i < 9; i++) { state.units.forEach(unit => { unit.done = true; }); turnCase(`${id} advance ${i}`, state, 'advance', config); }
}
for (const phase of ['playing', 'deployment', 'over'] as const) {
  const state = stateFor(); state.rules.turnOrder = 'reverse'; state.turnOrder = { policy: 'reverse', activeUnit: 3, data: { step: 1 } }; state.phase = phase;
  turnCase(`independent queries ${phase}`, state, 'query', { custom: true, independent: true, count: 2 });
}
{
  const state = stateFor(); state.rules.turnOrder = 'reverse'; state.phase = 'deployment'; state.units.forEach(unit => { unit.done = true; });
  turnCase('custom beginPlaying readies before initial policy', state, 'beginPlaying', { custom: true });
}
{
  const state = stateFor(); state.rules.turnOrder = 'reverse';
  turnCase('initial policy captured across changed state policy', state, 'start', { custom: true, switchToSide: true });
  turnCase('subsequent selection uses state policy not level rule', state, 'advance', { custom: true, switchToSide: true });
}
{
  const state = stateFor(); state.rules.turnOrder = 'reverse';
  turnCase('exhaust custom start', state, 'start', { custom: true, exhaust: true });
  turnCase('exhaust custom first advance', state, 'advance', { custom: true, exhaust: true });
  turnCase('exhaust custom concludes without round or claim', state, 'advance', { custom: true, exhaust: true });
}
{
  const state = stateFor(); state.rules.turnOrder = 'initiative'; turnCase('departed initiative start', state, 'start');
  const departed = state.turnOrder.activeUnit; state.units = state.units.filter(unit => unit.id !== departed); state.turnOrder.data['plugin.data'] = 9;
  turnCase('initiative prunes only charge of departed', state, 'advance');
}
const customReactions: ReactionBehavior[] = [
  { id: 'dodge', name: '闪避', hint: '自定义减伤', intercepts: false, incomingMultiplier: 0.4, retaliates: false, conservesResources: false },
  { id: 'ward', name: '护卫', hint: '自定义拦截', intercepts: true, incomingMultiplier: 0.2, retaliates: false, conservesResources: false },
  { id: 'revenge', name: '复仇', hint: '自定义反击', intercepts: false, incomingMultiplier: 1.3, retaliates: true, conservesResources: false },
  { id: 'thrift', name: '节用', hint: '自定义节用', intercepts: false, incomingMultiplier: 1, retaliates: true, conservesResources: true },
];
const reactions: Data[] = [];
function reactionCase(name: string, config: Data): void {
  const registry = Reactions.clone(), customPacks = clone(packs); registry.defineAll(customReactions);
  if (config.replaceGuard) registry.replace({ ...registry.get('guard'), name: '替换防御', incomingMultiplier: 0.25, retaliates: true });
  const catalog = createContentCatalog();
  if (config.weaponCost) {
    const weapon = customPacks.flatMap((pack: any) => pack.weapons ?? []).find((weapon: any) => weapon.id === 'soldier_sword');
    weapon.cooldown = 2;
  }
  new ContentPackInstaller(catalog).install(...customPacks);
  const engine = createBattleEngine({ content: catalog, reactions: registry }), state = stateFor();
  state.units[1].reaction = config.stance;
  if (config.interceptor) state.units[2].reaction = config.interceptor;
  else state.units = state.units.slice(0, 2);
  if (config.spent) for (const unit of state.units) unit.reactionUsedRound = state.turn;
  if (config.counterOff) state.rules.counterAttack = false;
  const before = clone(state), beforeForecast = hashState(state), result = forecast(engine.rules, state, state.units[0], state.units[1]);
  if (hashState(state) !== beforeForecast) throw new Error('Source forecast mutated state');
  const events: GameEvent[] = [], hashes: string[] = [];
  if (config.execute) {
    const plan = forecastCombatPlan(engine.rules, state, state.units[0], { x: 3, y: 3 });
    executeCombatPlan(engine.rules, state, plan, event => { events.push(event); state.scenario.eventCounts[event.type] = (state.scenario.eventCounts[event.type] ?? 0) + 1; hashes.push(hashState(state)); });
  }
  reactions.push({ name, config, before, result, events, hashes, after: state, hash: hashState(state) });
}
for (const stance of ['counter', 'guard', 'support', 'conserve', ...customReactions.map(value => value.id)]) {
  reactionCase(`${stance} forecast`, { stance });
  reactionCase(`${stance} execution`, { stance, execute: true });
  reactionCase(`${stance} spent reaction budget`, { stance, spent: true, execute: true });
}
reactionCase('replace default guard policy', { stance: 'guard', replaceGuard: true, execute: true });
reactionCase('custom interceptor takes damage not own multiplier', { stance: 'dodge', interceptor: 'ward', execute: true });
reactionCase('support interceptor retains source bonus', { stance: 'counter', interceptor: 'support', execute: true });
reactionCase('custom thrift rejects cooldown weapon', { stance: 'thrift', weaponCost: true, execute: true });
reactionCase('custom revenge accepts cooldown weapon', { stance: 'revenge', weaponCost: true, execute: true });
reactionCase('custom revenge obeys global counter flag', { stance: 'revenge', counterOff: true, execute: true });
const actions: Data[] = [];
const actionCases: [string, Action, GameState['phase']][] = [
  ['custom permitted wait', { kind: 'command', unit: 3, path: [{ x: 3, y: 4 }], command: { ability: 'wait' } }, 'playing'],
  ['custom refused same-owner wait', { kind: 'command', unit: 2, path: [{ x: 3, y: 3 }], command: { ability: 'wait' } }, 'playing'],
  ['custom stance action', { kind: 'reaction', unit: 3, stance: 'dodge' }, 'playing'],
  ['custom refused same-owner stance', { kind: 'reaction', unit: 2, stance: 'dodge' }, 'playing'],
  ['unknown stance action', { kind: 'reaction', unit: 3, stance: 'absent' }, 'playing'],
  ['custom end-turn action', { kind: 'endTurn' }, 'playing'],
  ['deployment cannot wait', { kind: 'command', unit: 3, path: [{ x: 3, y: 4 }], command: { ability: 'wait' } }, 'deployment'],
  ['over ignores command', { kind: 'command', unit: 3, path: [{ x: 3, y: 4 }], command: { ability: 'wait' } }, 'over'],
];
for (const [name, action, phase] of actionCases) {
  const turnOrders = TurnOrders.clone(); turnOrders.define(policy());
  const reactions = Reactions.clone(); reactions.defineAll(customReactions);
  const engine = createBattleEngine({ content, turnOrders, reactions }), state = stateFor();
  state.rules.turnOrder = 'reverse'; new BattleLifecycle(state, engine.rules).start(); state.phase = phase;
  const before = clone(state); let events: readonly GameEvent[] = [], error: Data | null = null;
  try { events = engine.dispatch(state, action); }
  catch (fault) { const value = fault as Error; error = { kind: value.name.replace('Error', ''), message: value.message }; }
  actions.push({ name, action, before, events, error, after: state, hash: hashState(state) });
}
const fixture = { packs, turns, reactions, actions, customReactions, defaults: { turnOrders: TurnOrders.all().map(value => ({ id: value.id, name: value.name })), reactions: Reactions.all() } };
const out = process.argv[2]; writeFileSync(out, out.endsWith('.gz') ? gzipSync(JSON.stringify(fixture)) : JSON.stringify(fixture));
console.log(`Generated ${turns.length} original TS turn-policy vectors and ${reactions.length} reaction vectors and ${actions.length} action vectors`);
