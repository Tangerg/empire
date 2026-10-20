import { writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { resourceConsumerOracle } from './consumer-oracle';
import {
  BattleResourceSystem, DefaultBattleResources, DefaultResourceAdapters, DefaultResourceSubjects,
  ResourceAdapterRegistry, ResourceSubjectResolverRegistry, canAffordTransactions,
} from '../../../packages/battle-engine/src/resources';

type Data = Record<string, any>;
const clone = <T>(value: T): T => structuredClone(value);
const squad = () => ({ code: 'squad-a', location: 'chest', stores: {
  supply: { balance: 5, limit: 8 }, fuel: { balance: 2.75, limit: null }, big: { balance: 4503599627370500, limit: null },
} });
const player = () => ({ id: 1, resources: { funds: { current: 12, capacity: null }, command_points: { current: 3, capacity: 5 } } });
const unit = () => ({ id: 7, resources: { momentum: { current: 2, capacity: 10 } },
  weaponState: { bow: { resources: { weapon_uses: { current: 3, capacity: 4 } } } }, squad: squad() });
function policies(trace: string[]): BattleResourceSystem {
  const adapters = DefaultResourceAdapters.clone();
  for (const [id, integer, aiWeight] of [['supply', true, 3.5], ['fuel', false, 0.25], ['big', true, 1]] as const) {
    adapters.register({ id, name: id.toUpperCase(), subjectKind: 'squad', integer, aiWeight,
      account: (subject: any) => {
        trace.push(`account:${id}`);
        const state = subject.squad.stores?.[id];
        return state ? {
          read: () => { trace.push(`read:${id}`); return { current: state.balance, capacity: state.limit }; },
          write: (current: number) => { trace.push(`write:${id}`); state.balance = current; },
        } : null;
      },
    } as any);
  }
  const subjects = DefaultResourceSubjects.clone().register({ kind: 'squad',
    subjectFor: (context: any, transaction: any) => {
      trace.push(`subject:${transaction.resource}:${transaction.amount}`);
      if (!context.unit.squad) throw new Error('fixture requires squad');
      return { kind: 'squad', squad: context.unit.squad };
    },
    ref: (subject: any) => { trace.push('ref:squad'); return { kind: 'squad', id: subject.squad.code, slot: subject.squad.location }; },
  } as any);
  return new BattleResourceSystem(adapters, subjects);
}
function subjectOf(kind: string, holder: Data): any {
  return kind === 'weapon' ? { kind, unit: holder, weapon: 'bow' } : { kind, [kind]: holder };
}
function numberOf(value: any): number {
  return typeof value === 'string' ? ({ NaN: NaN, Infinity: Infinity, '-Infinity': -Infinity } as Data)[value] : value;
}
function observe(call: () => unknown): Data {
  try { return { result: call() ?? null, error: null }; }
  catch (error) { const value = error as Error; return { result: null, error: { kind: value.name.replace('Error', ''), message: value.message } }; }
}
const cases: Data[] = [];
function check(name: string, operation: string, options: Data = {}): void {
  const input: Data = { name, operation, kind: 'squad', holder: squad(), ...options };
  const data = clone(input), trace: string[] = [], events: unknown[] = [];
  const resources = policies(trace), subject = subjectOf(data.kind, data.holder);
  if (data.replace) resources.adapters.replace({ ...resources.adapters.get(data.replace.id), ...data.replace });
  if (data.swapOnAccount) {
    const adapter = resources.adapters.get(data.resource);
    resources.adapters.replace({ ...adapter, account: subject => {
      resources.adapters.replace({ ...adapter, integer: false });
      return adapter.account(subject);
    } });
  }
  const result = observe(() => {
    switch (data.operation) {
      case 'inspect': return resources.inspect(data.resource, subject);
      case 'balance': return resources.balance(data.resource, subject);
      case 'has': return resources.hasAccount(data.resource, subject);
      case 'canSpend': return resources.canSpend(data.resource, subject, numberOf(data.requested));
      case 'credit': return resources.credit(data.resource, subject, numberOf(data.requested));
      case 'spend': return resources.spend(data.resource, subject, numberOf(data.requested));
      case 'afford': return resources.canAfford(data.costs, subject);
      case 'spendAll': return resources.spendAll(data.costs, subject);
      case 'value': return resources.planningValue(data.costs);
      case 'announce': resources.announce(subject, data.resource, data.amount, event => events.push(event)); return null;
      case 'ref': return resources.refOf(subject);
      case 'subject': return resources.refOf(resources.subjectFor(data.context, data.transaction));
      case 'transactions': return canAffordTransactions(resources, data.transactions, data.context);
      case 'accounts': return resources.adapters.keys().flatMap(id => resources.hasAccount(id, subject) ? [{ resource: id,
        name: resources.adapters.get(id).name, ...resources.inspect(id, subject) }] : []);
      default: throw new Error(`unknown fixture operation ${data.operation}`);
    }
  });
  cases.push({ ...input, ...result, after: data.holder, contextAfter: data.context ?? null, events, trace });
}
for (const [kind, resource, holder] of [['player', 'funds', player()], ['player', 'command_points', player()],
  ['unit', 'momentum', unit()], ['weapon', 'weapon_uses', unit()], ['squad', 'supply', squad()], ['squad', 'fuel', squad()]] as const) {
  const options = { kind, resource, holder };
  for (const operation of ['inspect', 'balance', 'has', 'accounts', 'ref']) check(`${kind} ${resource} ${operation}`, operation, options);
  for (const operation of ['credit', 'spend', 'canSpend']) for (const requested of [-3, 0, 0.49, 0.5, 1.5, 99, 'NaN', 'Infinity', '-Infinity']) {
    check(`${resource} ${operation} ${requested}`, operation, { ...options, requested });
  }
  check(`${resource} announce`, 'announce', { ...options, amount: -1 });
  check(`${resource} zero announcement`, 'announce', { ...options, amount: 0 });
}
for (const requested of [0.49999999999999994, 1.4999999999999998, 4503599627370497])
  check(`integer rounding boundary ${requested}`, 'spend', { resource: 'big', requested });
for (const operation of ['credit', 'spend', 'canSpend', 'announce']) {
  for (const requested of [3, 'NaN', 'Infinity']) check(`unlimited ${operation} ${requested}`, operation, {
    holder: { code: 'unlimited', location: 'reserve', stores: { supply: { balance: null, limit: 8 } } }, resource: 'supply', requested, amount: 3,
  });
}
for (const operation of ['has', 'inspect', 'balance', 'credit', 'spend', 'canSpend', 'announce']) {
  check(`unknown resource ${operation}`, operation, { resource: 'missing', requested: 'NaN', amount: 0 });
  check(`wrong subject ${operation}`, operation, { resource: 'funds', requested: 'NaN', amount: 0 });
  check(`missing account ${operation}`, operation, { resource: 'supply', holder: { code: 'empty', stores: {} }, requested: 1, amount: 0 });
}
check('unknown holder ref', 'ref', { kind: 'unknown' });
check('capacity below balance credit', 'credit', { resource: 'supply', requested: 1, holder: { stores: { supply: { balance: 5, limit: 3 } } } });
check('adapter replacement normalization', 'spend', { resource: 'supply', requested: 0.25, replace: { id: 'supply', integer: false, aiWeight: 12 } });
for (const operation of ['credit', 'spend', 'canSpend']) check(`account callback replaces policy ${operation}`, operation, { resource: 'supply', requested: 0.5, swapOnAccount: true });
check('large insufficient amount spelling', 'spend', { resource: 'supply', requested: 1e21 });
check('small insufficient amount spelling', 'spend', { resource: 'fuel', requested: 1e-7, holder: { stores: { fuel: { balance: 0, limit: null } } } });
check('large aggregate amount spelling', 'spendAll', { costs: [{ resource: 'supply', amount: 1e21 }] });
const costSets = {
  empty: [], repeated: [{ resource: 'supply', amount: 2 }, { resource: 'supply', amount: 4 }],
  mixed: [{ resource: 'fuel', amount: 0.25 }, { resource: 'supply', amount: 1.2 }, { resource: 'fuel', amount: 0.5 }, { resource: 'supply', amount: 0.3 }],
  'rounded after aggregation': [{ resource: 'supply', amount: 0.4 }, { resource: 'supply', amount: 0.4 }],
  'negative cancellation': [{ resource: 'supply', amount: 8 }, { resource: 'supply', amount: -6 }],
  'later insufficient': [{ resource: 'fuel', amount: 0.5 }, { resource: 'supply', amount: 6 }],
  'later unknown': [{ resource: 'fuel', amount: 0.5 }, { resource: 'unknown', amount: 1 }],
};
for (const [name, costs] of Object.entries(costSets)) for (const operation of ['afford', 'spendAll', 'value']) check(`${name} ${operation}`, operation, { costs });
check('replacement planning weight', 'value', { costs: [{ resource: 'supply', amount: 2 }, { resource: 'fuel', amount: 4 }], replace: { id: 'supply', aiWeight: -2 } });
for (const kind of ['player', 'unit', 'weapon', 'squad', 'missing']) check(`${kind} context resolution`, 'subject', {
  context: { player: player(), unit: unit(), weapon: 'bow' }, transaction: { subject: kind, resource: 'supply', amount: 2 },
});
check('missing player context', 'subject', { context: { unit: unit(), weapon: 'bow' }, transaction: { subject: 'player', resource: 'funds', amount: 2 } });
for (const [name, transactions] of Object.entries({
  'mixed holder transactions': [{ subject: 'player', resource: 'funds', amount: 4 }, { subject: 'squad', resource: 'supply', amount: 2 }, { subject: 'weapon', resource: 'weapon_uses', amount: 1 }],
  'duplicate transaction aggregates but resolver sees original': [{ subject: 'squad', resource: 'supply', amount: 1 }, { subject: 'squad', resource: 'supply', amount: 2 }],
  'duplicate transaction insufficient': [{ subject: 'squad', resource: 'supply', amount: 3 }, { subject: 'squad', resource: 'supply', amount: 3 }],
  'wrong holder transaction': [{ subject: 'squad', resource: 'funds', amount: 1 }],
  'missing adapter transaction': [{ subject: 'squad', resource: 'unknown', amount: 1 }],
  'missing holder transaction': [{ subject: 'unknown', resource: 'supply', amount: 1 }],
  'empty transactions': [],
})) check(name, 'transactions', { context: { player: player(), unit: unit(), weapon: 'bow' }, transactions });

const registries: Data[] = [];
const dummy = (id: string): any => ({ id, name: id, subjectKind: 'player', integer: true, aiWeight: 1, account: () => null });
const resolver = (kind: string): any => ({ kind, subjectFor: () => null, ref: () => null });
function registry(name: string, call: () => unknown): void { registries.push({ name, ...observe(call) }); }
registry('default adapter keys', () => DefaultResourceAdapters.keys());
registry('default subject keys', () => DefaultResourceSubjects.keys());
registry('adapter duplicate', () => new ResourceAdapterRegistry().register(dummy('a')).register(dummy('a')));
registry('subject duplicate', () => new ResourceSubjectResolverRegistry().register(resolver('a')).register(resolver('a')));
registry('adapter unknown', () => new ResourceAdapterRegistry().get('missing'));
registry('subject unknown', () => new ResourceSubjectResolverRegistry().get('missing' as any));
registry('adapter ask missing', () => new ResourceAdapterRegistry().tryGet('missing'));
registry('subject ask missing', () => new ResourceSubjectResolverRegistry().tryGet('missing' as any));
for (const id of ['', ' \t\r\n', '\uFEFF', '\u0085']) registry(`register id ${JSON.stringify(id)}`, () => new ResourceAdapterRegistry().register(dummy(id)).keys());
registry('blank replace permitted', () => new ResourceAdapterRegistry().replace(dummy(' ')).keys());
registry('blank clone rejected', () => new ResourceAdapterRegistry().replace(dummy(' ')).clone());
registry('blank register precedes sealed rejection', () => new ResourceAdapterRegistry().seal().register(dummy(' ')));
registry('sealed adapter register', () => new ResourceAdapterRegistry().seal().register(dummy('a')));
registry('sealed adapter replace', () => new ResourceAdapterRegistry().seal().replace(dummy('a')));
registry('sealed subject register', () => new ResourceSubjectResolverRegistry().seal().register(resolver('a')));
registry('sealed subject replace', () => new ResourceSubjectResolverRegistry().seal().replace(resolver('a')));
registry('adapter replace ordering', () => new ResourceAdapterRegistry().register(dummy('b')).register(dummy('a')).replace(dummy('b')).replace(dummy('c')).keys());
registry('subject replace ordering', () => new ResourceSubjectResolverRegistry().register(resolver('b')).register(resolver('a')).replace(resolver('b')).replace(resolver('c')).keys());
registry('detached keys and all', () => {
  const registry = new ResourceAdapterRegistry().register(dummy('a'));
  registry.keys().push('outside'); registry.all().splice(0);
  return { keys: registry.keys(), size: registry.size, has: registry.has('a') };
});
registry('resource clone isolation', () => {
  const original = DefaultBattleResources.clone().seal().seal(), cloned = original.clone();
  const adapterIdentity = cloned.adapters.get('funds') === original.adapters.get('funds');
  const resolverIdentity = cloned.subjects.get('player') === original.subjects.get('player');
  cloned.adapters.replace(dummy('funds')).register(dummy('new')); cloned.subjects.replace(resolver('player')).register(resolver('new'));
  return { originalSealed: original.adapters.isSealed && original.subjects.isSealed,
    cloneSealed: cloned.adapters.isSealed || cloned.subjects.isSealed, adapterIdentity, resolverIdentity,
    originalAdapters: original.adapters.keys(), clonedAdapters: cloned.adapters.keys(), originalSubjects: original.subjects.keys(), clonedSubjects: cloned.subjects.keys(),
    originalFundsName: original.adapters.get('funds').name };
});
registry('default resource independence', () => {
  const left = DefaultBattleResources.clone(), right = DefaultBattleResources.clone();
  left.adapters.replace(dummy('funds'));
  return { leftName: left.adapters.get('funds').name, rightName: right.adapters.get('funds').name,
    defaultName: DefaultBattleResources.adapters.get('funds').name };
});
const consumerOracle = resourceConsumerOracle();
const output = JSON.stringify({ cases, registries, ...consumerOracle }, null, 2) + '\n';
writeFileSync(process.argv[2], process.argv[2].endsWith('.gz') ? gzipSync(output) : output);
console.log(`Generated ${cases.length} resource-policy cases and ${registries.length} registry and ${consumerOracle.consumers.length} consumer cases from source`);
