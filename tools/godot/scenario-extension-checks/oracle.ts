import { readFileSync, writeFileSync } from 'node:fs';
const root = process.env.EMPIRE_SOURCE_ROOT ?? process.cwd();
const { ScenarioConditionHandlers, ScenarioEffectHandlers, runScenarioTriggers } = await import(`${root}/packages/battle-engine/src/scenario.ts`);
const { ObjectiveHandlers } = await import(`${root}/packages/battle-engine/src/objective-system.ts`);
const { refreshObjectiveStates } = await import(`${root}/packages/battle-engine/src/victory.ts`);
const { createBattleEngine } = await import(`${root}/packages/battle-engine/src/plugins/default.ts`);
const { ContentPackInstaller, createContentCatalog } = await import(`${root}/packages/battle-engine/src/content-pack.ts`);
const { PayloadReferences } = await import(`${root}/packages/battle-engine/src/payload-references.ts`);
const { hashState } = await import(`${root}/packages/battle-engine/src/replay.ts`);
const packs = JSON.parse(readFileSync(`${root}/godot/data/content-packs.json`, 'utf8'));
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const original = JSON.parse(readFileSync(`${root}/godot/tests/oracle/grayflag-initial-raw.json`, 'utf8')).state;
const baseline = createBattleEngine({ content }).rules;
const conditions = ScenarioConditionHandlers.clone().register({ kind: 'testGate', children: value => [value.child],
  references: value => new PayloadReferences().zone(value.zone), evaluate: (context, value) => context.evaluate(value.child) });
const effects = ScenarioEffectHandlers.clone().replace({ kind: 'setVariable', apply: ({ state }, value) => { state.scenario.variables[value.key] = value.value * 2; } })
  .register({ kind: 'testAward', references: value => new PayloadReferences().zone(value.zone), apply: (context, value) => {
    context.rules.scenarioEffects.apply(context.rules, context.state, { type: 'setVariable', key: 'score', value: value.amount }, context.emit);
    context.emit({ type: 'scenarioSignal', signal: 'awarded' });
  } });
const objectives = ObjectiveHandlers.clone().register({ kind: 'testGoal', role: 'optional', label: 'Custom goal',
  references: value => new PayloadReferences().zone(value.zone), outcome: context => context.state.scenario.variables.score >= 12 ? 'success' : 'pending',
  progress: context => `score=${context.state.scenario.variables.score ?? 0}` })
  .register({ kind: 'testSelf', label: 'Self only', children: value => value.objectives,
    outcome: () => 'pending', progress: () => 'self' });
const rules = { ...baseline, scenarioConditions: conditions, scenarioEffects: effects, objectives };
const state = structuredClone(original);
for (const owner of state.players) { owner.objectives = []; owner.objectiveStates = {}; }
state.players[0].objectives = [{ type: 'testGoal', id: 'goal', zone: 'near' }, { type: 'testSelf', id: 'self', objectives: [{ type: 'surviveTurns', id: 'child', turns: 0 }] }];
state.players[0].objectiveStates = Object.fromEntries(['goal', 'self', 'child'].map(id => [id, { id, status: 'active', hidden: false }]));
state.scenario.variables = {}; state.scenario.zones = { near: [{ x: 0, y: 0 }] };
state.scenario.firedTriggerIds = []; state.scenario.triggerRuntime = {};
state.scenario.triggers = [
  { id: 'dependent', timing: 'afterAction', condition: { type: 'variable', key: 'score', op: 'gte', value: 12 }, effects: [{ type: 'emitSignal', signal: 'dependent' }] },
  { id: 'award', timing: 'afterAction', condition: { type: 'testGate', zone: 'near', child: { type: 'turnAtLeast', turn: 1 } }, effects: [{ type: 'testAward', zone: 'near', amount: 6 }] },
];
const initial = structuredClone(state), events = [], hashes = [];
const emit = event => { events.push(event); hashes.push(hashState(state)); };
const beforeProgress = objectives.progress(rules, state, 1, state.players[0].objectives[0]);
runScenarioTriggers(rules, state, 'afterAction', emit); refreshObjectiveStates(rules, state, emit);
runScenarioTriggers(rules, state, 'afterAction', emit); refreshObjectiveStates(rules, state, emit);
const metadata = {
  conditionChildren: conditions.children(state.scenario.triggers[1].condition), conditionReferences: conditions.references(state.scenario.triggers[1].condition),
  effectReferences: effects.references(state.scenario.triggers[1].effects[0]), objectiveReferences: objectives.references(state.players[0].objectives[0]),
  description: objectives.describe(state.players[0].objectives[0]), role: objectives.role(state.players[0].objectives[0]), refresh: objectives.refreshMode(state.players[0].objectives[1]),
  beforeProgress, afterProgress: objectives.progress(rules, state, 1, state.players[0].objectives[0]),
};
const edgeState = structuredClone(initial); edgeState.units = edgeState.units.slice(0, 1);
const edgeConditions = [-10, 0, 0.5, 1, 2].map(multiplier => {
  edgeState.units[0].hp = content.units.get(edgeState.units[0].type).maxHp * multiplier;
  const condition = { type: 'unitHealth', selector: {}, aggregate: 'all', op: 'eq', value: Math.max(0, Math.min(1, multiplier)) };
  return { hp: edgeState.units[0].hp, condition, result: conditions.evaluate(edgeState, condition, content) };
});
const edgeMaxima = [0, -1].map(maximum => {
  const probeContent = { ...content, units: { get: id => ({ ...content.units.get(id), maxHp: maximum }) } };
  const condition = { type: 'unitHealth', selector: {}, aggregate: 'all', op: 'eq', value: 1 };
  return { maximum, condition, result: conditions.evaluate(edgeState, condition, probeContent) };
});
const metadataCases = [];
function inspectCondition(payload) {
  metadataCases.push({ family: 'condition', payload, references: ScenarioConditionHandlers.references(payload), children: ScenarioConditionHandlers.children(payload) });
  for (const child of ScenarioConditionHandlers.children(payload)) inspectCondition(child);
}
function inspectObjective(payload) {
  metadataCases.push({ family: 'objective', payload, references: ObjectiveHandlers.references(payload), children: ObjectiveHandlers.children(payload),
    role: ObjectiveHandlers.role(payload), refresh: ObjectiveHandlers.refreshMode(payload), description: ObjectiveHandlers.describe(payload) });
  for (const child of ObjectiveHandlers.children(payload)) inspectObjective(child);
  for (const condition of ObjectiveHandlers.references(payload).conditions) inspectCondition(condition);
}
for (const level of JSON.parse(readFileSync(`${root}/godot/data/grayflag-campaign-levels.json`, 'utf8'))) {
  for (const player of level.players) for (const objective of player.objectives ?? []) inspectObjective(objective);
  for (const trigger of level.scenario?.triggers ?? []) {
    inspectCondition(trigger.condition);
    for (const payload of trigger.effects) metadataCases.push({ family: 'effect', payload, references: ScenarioEffectHandlers.references(payload) });
  }
}
inspectCondition({ type: 'turnCycle', every: 0 });
inspectObjective({ type: 'protect', selector: { zone: 'missing' }, minimumAlive: 0, untilTurn: 0 });
inspectObjective({ type: 'escort', selector: {}, zone: 'missing', count: 0 });
inspectObjective({ type: 'neutralizeComposite', composite: 'boss', minimumNeutralized: 0.5 });
inspectObjective({ type: 'neutralizeComposite', composite: 'boss' });
inspectObjective({ type: 'neutralizeComposite', composite: 'boss', minimumNeutralized: null });
for (const payload of [
  { type: 'teleportUnits', zone: 'destination', selector: { zone: 'selected' } },
  { type: 'restoreWithdrawnUnits', zone: 'destination', selector: { zone: 'selected' } },
  { type: 'forceMove', selector: { zone: 'missing' }, source: { x: -1, y: 1 }, distance: -1 },
  { type: 'setElevation', zone: 'missing', value: 0.5 }, { type: 'addElevation', zone: 'missing', amount: 0.5 },
  { type: 'addEngagementRule', rule: { id: '  ', zone: 'missing', players: [2, 99] } },
  { type: 'addEngagementRule', rule: { id: '\uFEFF', zone: 'near' } },
  { type: 'addEngagementRule', rule: { id: ' \uFEFF\t', zone: 'near' } },
  { type: 'addEngagementRule', rule: { id: '\u0085', zone: 'near' } },
  { type: 'addEngagementRule', rule: { id: '\u180E', zone: 'near' } },
]) metadataCases.push({ family: 'effect', payload, references: ScenarioEffectHandlers.references(payload) });
writeFileSync(process.env.EMPIRE_SCENARIO_EXTENSION_OUTPUT!, JSON.stringify({ packs, initial, after: state, events, hashes, metadata, metadataCases, edgeConditions, edgeMaxima,
  keys: { conditions: ScenarioConditionHandlers.keys(), effects: ScenarioEffectHandlers.keys(), objectives: ObjectiveHandlers.keys() } }, null, 2));
console.log('Source extension oracle: custom condition → replaced built-in effect → custom objective, metadata and self-refresh');
