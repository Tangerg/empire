import { writeFileSync } from 'node:fs';
import {
  CampaignConditionRegistry, CampaignEffectRegistry, CampaignNodeRegistry, CampaignRuntime,
  createCampaignRuleServices, createCampaignState, DefaultCampaignConditions,
  DefaultCampaignEffects, DefaultCampaignNodes,
} from '../../../packages/campaign-engine/src/index';

type Data = Record<string, any>;
const definition: any = {
  schema: 1, id: 'rule-checks', version: 1, start: 'story', contentPacks: {},
  nodes: [
    { id: 'story', type: 'story', presentation: 'story', next: 'choice' },
    { id: 'choice', type: 'choice', presentation: 'choice', choices: [
      { id: 'open', next: 'battle' },
      { id: 'locked', next: 'battle', condition: { type: 'flag', flag: 'unlock' } },
    ] },
    { id: 'battle', type: 'battle', level: 'arena', next: {} },
    { id: 'ending', type: 'ending', presentation: 'ending', outcome: 'completed' },
  ],
  initial: {
    flags: ['ready'], features: ['veteran'], variables: { count: 3, label: 'hero', enabled: true },
    resources: { gold: 8 }, relations: { ally: -2 },
    roster: [{ id: 'hero', unitType: 'soldier', owner: 1 }],
  },
};
const state = createCampaignState(definition);
function errorOf(error: unknown): Data {
  const value = error as Error;
  return { name: value.name, message: value.message };
}
function observe(call: () => unknown): Data {
  try { return { value: call() ?? null, error: null }; }
  catch (error) { return { value: null, error: errorOf(error) }; }
}
const conditions: Data[] = [];
function condition(name: string, value: Data): void {
  conditions.push({ name, condition: value, ...observe(() => DefaultCampaignConditions.evaluate(state, value as any)) });
}
for (const flag of ['ready', 'missing']) {
  condition(`flag ${flag} default`, { type: 'flag', flag });
  for (const present of [true, false]) condition(`flag ${flag} ${present}`, { type: 'flag', flag, present });
}
for (const [key, values] of Object.entries({ count: [2, 3, 4, '3'], label: ['hero', 'other', 3], enabled: [true, false, 1], missing: [0, false, ''] }))
  for (const value of values) for (const op of ['eq', 'neq', 'gte', 'lte'])
    condition(`variable ${key} ${op} ${JSON.stringify(value)}`, { type: 'variable', key, op, value });
for (const type of ['resource', 'relation']) for (const exists of [true, false])
  for (const op of ['gte', 'lte']) for (const value of [-3, -2, 0, 8, 9])
    condition(`${type} ${exists} ${op} ${value}`, {
      type, [type === 'resource' ? 'resource' : 'faction']: exists ? (type === 'resource' ? 'gold' : 'ally') : 'missing', op, value,
    });
for (const unit of ['hero', 'missing']) {
  condition(`roster ${unit}`, { type: 'roster', unit });
  for (const disposition of ['available', 'fallen', 'routed', 'surrendered', 'missing'])
    condition(`roster ${unit} ${disposition}`, { type: 'roster', unit, disposition });
}
const yes = { type: 'flag', flag: 'ready' }, no = { type: 'flag', flag: 'missing' }, bad = { type: 'test.unknown' };
for (const type of ['all', 'any']) for (const children of [[], [yes], [no], [yes, no], [no, yes]])
  condition(`${type} ${JSON.stringify(children)}`, { type, conditions: children });
condition('all short circuits', { type: 'all', conditions: [no, bad] });
condition('any short circuits', { type: 'any', conditions: [yes, bad] });
condition('not true', { type: 'not', condition: yes });
condition('not false', { type: 'not', condition: no });
condition('nested boolean algebra', { type: 'all', conditions: [yes, { type: 'any', conditions: [no, { type: 'not', condition: no }] }] });
condition('unknown condition rejects', bad);

const effects: Data[] = [];
function effect(name: string, operations: Data[]): void {
  const after = structuredClone(state);
  const result = observe(() => { for (const value of operations) DefaultCampaignEffects.apply(after, value as any); });
  effects.push({ name, effects: operations, ...result, after });
}
for (const value of ['new', 0, -3.5, true, false]) effect(`set variable ${value}`, [{ type: 'setVariable', key: 'count', value }]);
for (const key of ['count', 'new', 'label', 'enabled']) effect(`add variable ${key}`, [{ type: 'addVariable', key, amount: -2 }]);
for (const flag of ['ready', 'missing']) {
  effect(`set flag ${flag} idempotent`, [{ type: 'setFlag', flag }, { type: 'setFlag', flag }]);
  effect(`clear flag ${flag}`, [{ type: 'clearFlag', flag }]);
}
for (const resource of ['gold', 'missing']) effect(`add resource ${resource}`, [{ type: 'addResource', resource, amount: -10 }]);
for (const faction of ['ally', 'missing']) effect(`change relation ${faction}`, [{ type: 'changeRelation', faction, amount: 5 }]);
for (const feature of ['veteran', 'missing']) for (const enabled of [true, false])
  effect(`feature ${feature} ${enabled}`, [{ type: 'setFeature', feature, enabled }, { type: 'setFeature', feature, enabled }]);
for (const disposition of ['available', 'fallen', 'routed', 'surrendered', 'missing'])
  effect(`roster disposition ${disposition}`, [{ type: 'setRosterDisposition', unit: 'hero', disposition }]);
effect('missing roster rejects', [{ type: 'setRosterDisposition', unit: 'missing', disposition: 'fallen' }]);
effect('unknown effect rejects', [{ type: 'test.unknown' }]);
effect('standalone effect application is not a transaction', [{ type: 'setFlag', flag: 'applied' }, { type: 'addVariable', key: 'label', amount: 1 }]);

const runtimes: Data[] = [];
function runtimeCase(name: string, start: string, operations: Data[], change?: (d: Data, s: Data) => void): void {
  const d = structuredClone(definition), s: Data = structuredClone(state);
  s.currentNode = start;
  change?.(d, s);
  const runtime = new CampaignRuntime(d as any, s as any);
  const steps = operations.map(operation => ({ ...operation, ...observe(() => {
    switch (operation.op) {
      case 'advance': return runtime.advance();
      case 'choose': return runtime.choose(operation.id);
      case 'choices': return runtime.choices();
      case 'complete': return runtime.completeBattle(operation.result);
      default: throw new Error(`unknown fixture operation ${operation.op}`);
    }
  }), after: runtime.snapshot() }));
  runtimes.push({ name, definition: d, before: s, steps });
}
runtimeCase('choice availability and errors', 'choice', [{ op: 'choices' }, { op: 'choose', id: 'locked' }, { op: 'choose', id: 'missing' }, { op: 'advance' }, { op: 'choose', id: 'open' }, { op: 'choices' }]);
runtimeCase('unlocked choice', 'choice', [{ op: 'choices' }, { op: 'choose', id: 'locked' }], (_d, s) => s.flags.push('unlock'));
runtimeCase('choose only accepts choice nodes', 'story', [{ op: 'choose', id: 'open' }, { op: 'advance' }]);
runtimeCase('battle requires explicit API', 'battle', [{ op: 'advance' }]);
const lateFailure = [{ type: 'setFlag', flag: 'must-rollback' }, { type: 'addVariable', key: 'label', amount: 1 }];
runtimeCase('story late effect rollback is repeatable', 'story', [{ op: 'advance' }, { op: 'advance' }], d => d.nodes[0].effects = lateFailure);
runtimeCase('choice node and selected effects rollback together', 'choice', [{ op: 'choose', id: 'open' }, { op: 'choices' }], d => {
  d.nodes[1].effects = [{ type: 'addResource', resource: 'gold', amount: 10 }];
  d.nodes[1].choices[0].effects = lateFailure;
});
runtimeCase('ending late effect rollback', 'ending', [{ op: 'advance' }], d => d.nodes[3].effects = lateFailure);
runtimeCase('ending completes once', 'ending', [{ op: 'advance' }, { op: 'advance' }, { op: 'choose', id: 'open' }]);
runtimeCase('failed ending', 'ending', [{ op: 'advance' }, { op: 'advance' }], d => d.nodes[3].outcome = 'failed');
for (const status of ['completed', 'failed']) runtimeCase(`terminal ${status} rejects writes but retains choice projection`, 'choice',
  [{ op: 'advance' }, { op: 'choose', id: 'open' }, { op: 'choices' }], (_d, s) => s.status = status);
const result = (outcome: string): any => ({ requestId: 'rule-checks:battle:1', outcome, winnerTeam: outcome === 'victory' ? 1 : null,
  reason: 'test', turns: 4, units: [], signals: ['signal'], eventCounts: {} });
const pending = (_d: Data, s: Data): void => { s.pendingBattle = { requestId: 'rule-checks:battle:1', node: 'battle', level: 'arena' }; s.battleSequence = 1; };
for (const outcome of ['victory', 'defeat', 'retreat']) {
  runtimeCase(`no-next ${outcome}`, 'battle', [{ op: 'complete', result: result(outcome) }, { op: 'complete', result: result(outcome) }], pending);
  runtimeCase(`mapped ${outcome}`, 'battle', [{ op: 'complete', result: result(outcome) }, { op: 'advance' }], (d, s) => {
    pending(d, s); d.nodes[2].next[outcome] = 'ending';
    d.nodes[2].outcomeEffects = { [outcome]: [{ type: 'setFlag', flag: outcome }] };
  });
}
runtimeCase('complete without pending battle', 'battle', [{ op: 'complete', result: result('victory') }]);
runtimeCase('wrong request preserves pending battle', 'battle', [{ op: 'complete', result: { ...result('victory'), requestId: 'wrong' } }, { op: 'complete', result: result('victory') }], pending);
const projected = { campaignUnit: 'hero', disposition: 'routed', hpRatio: 0.25, moraleRatio: 0.3, unitType: 'archer', rank: 2,
  rankProgress: 10, resources: { mana: { current: 2, capacity: 5 } }, career: null, unlockedCareers: ['scout'], careerMastery: { scout: 4 }, learnedAbilities: ['aim'], tags: [] };
runtimeCase('battle projection rolls back after late outcome failure', 'battle', [{ op: 'complete', result: { ...result('victory'), units: [projected] } }], (d, s) => {
  pending(d, s); d.nodes[2].outcomeEffects = { victory: lateFailure };
});
runtimeCase('battle projection ignores unknown roster ids', 'battle', [{ op: 'complete', result: { ...result('victory'), units: [projected, { ...projected, campaignUnit: 'missing' }] } }], pending);

function registryChecks(family: string): Data[] {
  const factory = (): any => family === 'condition' ? new CampaignConditionRegistry() : family === 'effect' ? new CampaignEffectRegistry() : new CampaignNodeRegistry();
  const handler = (kind: string): any => ({ kind, evaluate: () => true, apply: () => {}, advance: () => {} });
  const registry = factory(); const checks: Data[] = [];
  const check = (name: string, call: () => unknown): void => { checks.push({ name, ...observe(call) }); };
  check('empty registry', () => ({ count: registry.size, keys: registry.keys(), all: registry.all().map((entry: any) => entry.kind), sealed: registry.isSealed }));
  check('register preserves insertion order', () => { registry.register(handler('z')); registry.register(handler('a')); return registry.keys(); });
  check('duplicate rejects', () => registry.register(handler('z')));
  check('lookup distinguishes query and demand', () => ({ present: registry.has('a'), absent: registry.has('missing'), optional: registry.tryGet('missing') ?? null, found: registry.get('a').kind }));
  check('unknown lookup rejects', () => registry.get('missing'));
  check('replace existing and introduce new', () => { registry.replace(handler('z')); registry.replace(handler('new')); return registry.keys(); });
  check('seal prevents register', () => { registry.seal(); return registry.register(handler('late')); });
  check('seal prevents replace', () => registry.replace(handler('a')));
  check('clone is independent and mutable', () => { const clone = registry.clone(); clone.register(handler('clone-only')); return { original: registry.keys(), cloned: clone.keys(), originalSealed: registry.isSealed, cloneSealed: clone.isSealed, count: clone.size }; });
  return checks;
}
const registries = ['condition', 'effect', 'node'].map(family => ({ family, checks: registryChecks(family) }));
function extensions(): Data {
  const rules = createCampaignRuleServices();
  rules.conditions.register({ kind: 'test.threshold', evaluate: (s: any, c: any) => s.resources.gold >= c.minimum } as any);
  rules.effects.register({ kind: 'test.bonus', apply: (s: any, e: any) => { s.resources.gold += e.amount * 2; } } as any);
  rules.nodes.register({ kind: 'test.camp', advance: (context: any, node: any) => { context.apply(node.effects); context.moveTo(node.next); },
    validate: (inspection: any, node: any) => { if (!inspection.hasNode(node.next)) inspection.reject(`camp target "${node.next}" is absent`); } } as any);
  const d: any = structuredClone(definition); d.nodes[0].type = 'test.camp'; d.nodes[0].effects = [{ type: 'test.bonus', amount: 5 }];
  d.nodes[1].choices[1].condition = { type: 'all', conditions: [{ type: 'test.threshold', minimum: 15 }, { type: 'not', condition: { type: 'flag', flag: 'absent' } }] };
  const runtime = new CampaignRuntime(d, undefined, rules);
  rules.conditions.replace({ kind: 'test.threshold', evaluate: () => false } as any);
  rules.effects.replace({ kind: 'test.bonus', apply: () => { throw new Error('mutated external effect'); } } as any);
  rules.nodes.replace({ kind: 'test.camp', advance: () => { throw new Error('mutated external node'); } } as any);
  const checks = [observe(() => runtime.advance()), observe(() => runtime.choices()), observe(() => runtime.choose('locked'))];
  const badDefinition = structuredClone(d); badDefinition.nodes[0].next = 'missing';
  const rejected = observe(() => new CampaignRuntime(badDefinition, undefined, rules));
  const validating = createCampaignRuleServices(); validating.nodes.register({ kind: 'test.camp', advance: () => {},
    validate: (inspection: any, node: any) => { if (!inspection.hasNode(node.next)) inspection.reject(`camp target "${node.next}" is absent`); } } as any);
  const validation = observe(() => new CampaignRuntime(badDefinition, undefined, validating));
  return { checks, after: runtime.snapshot(), replacedHandlerHasNoValidator: rejected.error === null, validation };
}
writeFileSync(process.argv[2], JSON.stringify({ state, conditions, effects, runtimes, registries, extensions: extensions(), defaultSeals: {
  conditions: DefaultCampaignConditions.isSealed, effects: DefaultCampaignEffects.isSealed, nodes: DefaultCampaignNodes.isSealed,
} }, null, 2));
console.log(`Campaign rules oracle: ${conditions.length} conditions, ${effects.length} effects, ${runtimes.reduce((sum, value) => sum + value.steps.length, 0)} runtime steps, 27 registry checks, custom extensions.`);
