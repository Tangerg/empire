import { writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { KeyedRegistry, PriorityRegistry } from '../../../packages/battle-engine/src/registry';
import { ActionHandlerRegistry } from '../../../packages/battle-engine/src/action-system';
import { AiIntentRegistry } from '../../../packages/battle-engine/src/ai/turn-context';

type Entry = { id: string; priority: number; payload: { value: number } };
class Rules extends KeyedRegistry<string, Entry> {
  constructor(subject: string) { super(subject); }
  protected keyOf(entry: Entry): string { return entry.id; }
  clone(): Rules { return this.copyInto(new Rules(this.subject)); }
}
class Priorities extends PriorityRegistry<Entry> {
  constructor() { super('priority rule'); }
  clone(): Priorities { return this.copyInto(new Priorities()); }
}
const entry = (id: string, priority = 0): Entry => ({ id, priority, payload: { value: 1 } });
type Check = { name: string; value: unknown; error: { name: string; message: string } | null };
function recorder() {
  const checks: Check[] = [];
  return { checks, check(name: string, call: () => unknown): void {
    try { checks.push({ name, value: structuredClone(call() ?? null), error: null }); }
    catch (error) { const caught = error as Error; checks.push({ name, value: null, error: { name: caught.name, message: caught.message } }); }
  } };
}
const subjects = ['tactical grid', 'weapon area shape', 'unit directive', 'action handler', 'AI intent', 'AI ability evaluator', 'AI objective advisor',
  'campaign condition handler', 'campaign effect handler', 'campaign node handler', 'test rule'];
function keyed(subject: string) {
  const registry = new Rules(subject), { checks, check } = recorder();
  const z = entry('z'), a = entry('a'), replacement = entry('z');
  check('empty', () => ({ count: registry.size, keys: registry.keys(), all: registry.all(), sealed: registry.isSealed }));
  check('query absent', () => ({ has: registry.has('missing'), optional: registry.tryGet('missing') ?? null }));
  check('demand absent', () => registry.get('missing'));
  check('fluent register', () => registry.register(z) === registry && registry.register(a) === registry);
  check('entry identities and order', () => ({ keys: registry.keys(), identities: registry.all()[0] === z && registry.all()[1] === a,
    get: registry.get('a') === a, optional: registry.tryGet('a') === a, has: registry.has('a'), count: registry.size }));
  check('duplicate rejects', () => registry.register(entry('z')));
  check('replace retains position', () => ({ same: registry.replace(replacement) === registry, keys: registry.keys(), entry: registry.get('z') === replacement }));
  check('replace introduces key', () => ({ same: registry.replace(entry('new')) === registry, keys: registry.keys(), count: registry.size }));
  const keys = registry.keys(), all = registry.all();
  check('detached snapshots', () => { registry.register(entry('later')); return { keys, count: all.length, first: all[0] === replacement, current: registry.keys() }; });
  check('collection edits cannot change table', () => { keys.reverse(); keys[0] = 'forged'; all.reverse(); all[0] = entry('forged'); return registry.keys(); });
  check('seal is fluent and idempotent', () => ({ same: registry.seal() === registry && registry.seal() === registry, sealed: registry.isSealed }));
  check('sealed duplicate rejects before duplicate check', () => registry.register(entry('a')));
  check('sealed new register rejects', () => registry.register(entry('late')));
  check('sealed existing replace rejects', () => registry.replace(entry('a')));
  check('sealed new replace rejects', () => registry.replace(entry('late')));
  const clone = registry.clone();
  check('clone from sealed is mutable and shallow', () => ({ sealed: clone.isSealed, originalSealed: registry.isSealed,
    keys: clone.keys(), count: clone.size, shared: clone.all().every((value, index) => value === registry.all()[index]) }));
  check('clone table mutations are independent', () => { clone.replace(entry('a')); clone.register(entry('clone-only')); return {
    original: registry.keys(), cloned: clone.keys(), originalEntry: registry.get('a') === a, cloneReplaced: clone.get('a') !== a,
    untouchedShared: clone.get('z') === registry.get('z') }; });
  return { subject, checks };
}
function priorityChecks() {
  const registry = new Priorities(), { checks, check } = recorder();
  check('empty ordered cached and read only', () => ({ keys: registry.ordered().map(e => e.id), cached: registry.ordered() === registry.ordered(), frozen: Object.isFrozen(registry.ordered()) }));
  for (const id of ['a.', '\ue000', '\ud800\udc00', 'Z', 'a-', '$', '\ud83d\ude00', 'a']) registry.register(entry(id, 0.25));
  registry.register(entry('negative', -1.5)); registry.register(entry('fraction', 0.125)); registry.register(entry('last', 10.5));
  check('fractional priority and UTF-16 ties', () => registry.ordered().map(e => e.id));
  const prior = registry.ordered();
  check('replace invalidates cache and changes order', () => { registry.replace(entry('last', -2.75)); return { keys: registry.ordered().map(e => e.id), changed: prior !== registry.ordered(), old: prior.map(e => e.id) }; });
  const beforeRegister = registry.ordered();
  check('register invalidates cache', () => { registry.register(entry('new', -3.5)); return { changed: beforeRegister !== registry.ordered(), first: registry.ordered()[0].id }; });
  const beforeDuplicate = registry.ordered();
  check('duplicate still rejects', () => registry.register(entry('new')));
  check('failed register invalidates cache without changing table', () => ({ changed: beforeDuplicate !== registry.ordered(), keys: registry.keys() }));
  registry.seal(); const sealedOrder = registry.ordered();
  check('sealed priority replacement rejects', () => registry.replace(entry('new')));
  check('failed sealed write invalidates cache', () => ({ changed: sealedOrder !== registry.ordered(), keys: registry.ordered().map(e => e.id) }));
  check('ordered list cannot be modified', () => { let rejected = false; try { (registry.ordered() as Entry[])[0] = entry('forged'); } catch { rejected = true; } return rejected; });
  const first = entry('first'), second = entry('second');
  const stable = new Priorities().register(first).register(second); second.id = 'first';
  check('comparator ties preserve insertion order', () => ({ first: stable.ordered()[0] === first, second: stable.ordered()[1] === second }));
  const clone = registry.clone();
  check('priority clone is independent mutable shallow', () => { const original = registry.get('new'); clone.replace(entry('new', 99)); return {
    sealed: clone.isSealed, originalSame: registry.get('new') === original, shared: clone.get('fraction') === registry.get('fraction'),
    originalFirst: registry.ordered()[0].id, cloneFirst: clone.ordered()[0].id }; });
  const sharing = new Priorities().register(entry('payload')).seal(), copy = sharing.clone();
  check('clone keeps mutable entry identity', () => { copy.get('payload').payload.value = 7; return { same: copy.get('payload') === sharing.get('payload'), value: sharing.get('payload').payload.value }; });
  const choose = new AiIntentRegistry();
  const visited: string[] = [];
  choose.register({ id: 'late', priority: 0.75, propose: () => { visited.push('late'); return { kind: 'endTurn' }; } });
  choose.register({ id: 'early', priority: -0.25, propose: () => { visited.push('early'); return null; } });
  choose.register({ id: 'middle', priority: 0.125, propose: () => { visited.push('middle'); return { kind: 'endTurn' }; } });
  check('fractional ordering reaches AI dispatch', () => ({ action: choose.choose(null as never), visited }));
  return checks;
}
const numericCases: { name: string; entries: [string, number][]; portable?: boolean }[] = [
  { name: 'infinities and finite fractions', entries: [['plus.z', Infinity], ['negative', -0.5], ['minus.z', -Infinity], ['plus.a', Infinity], ['zero', -0], ['minus.a', -Infinity]] },
  { name: 'both NaN use ID', entries: [['z', NaN], ['a', NaN]] },
  { name: 'NaN left uses ID', entries: [['z', NaN], ['a', 1]] },
  { name: 'NaN right uses ID', entries: [['z', 1], ['a', NaN]] },
  { name: 'signed zero uses ID', entries: [['z', -0], ['a', 0]] },
];
for (const length of [3, 16, 17, 64, 96]) {
  for (let rotation = 0; rotation < 4; rotation++) {
    const priorities = [2, NaN, 1, Infinity, -Infinity, -0, 0.125];
    const mixed: [string, number][] = Array.from({ length }, (_, i) => [`rule.${String(i).padStart(3, '0')}`, i < 3 ? [2, NaN, 1][i] : priorities[(length - i + rotation) % priorities.length]]);
    const offset = rotation % length;
    numericCases.push({ name: `mixed non-finite ${length} entries rotation ${rotation}`, portable: false,
      entries: [...mixed.slice(offset), ...mixed.slice(0, offset)] });
    numericCases.push({ name: `finite ${length} entries rotation ${rotation}`, portable: true,
      entries: Array.from({ length }, (_, i) => [`rule.${String(i).padStart(3, '0')}`, ((length - i + rotation) % 9 - 4) / 8]) });
  }
}
const numeric = numericCases.map(test => {
  const registry = new Priorities(); for (const [id, priority] of test.entries) registry.register(entry(id, priority));
  return { name: test.name, portable: test.portable ?? true, entries: test.entries.map(([id, priority]) => ({ id, priority: Number.isNaN(priority) ? 'NaN' : priority === Infinity ? '+Infinity' : priority === -Infinity ? '-Infinity' : priority })), expected: registry.ordered().map(e => e.id) };
});
const nonTransitive = [['a', 2], ['b', NaN], ['c', 1]] as const;
const compare = (left: typeof nonTransitive[number], right: typeof nonTransitive[number]) => left[1] - right[1] || (left[0] < right[0] ? -1 : left[0] > right[0] ? 1 : 0);
const nonFiniteCharacterization = { acceptsNaN: true, witness: [{ id: 'a', priority: 2 }, { id: 'b', priority: 'NaN' }, { id: 'c', priority: 1 }],
  specification: 'https://tc39.es/ecma262/multipage/indexed-collections.html#sec-sortindexedproperties', cyclicComparison: [compare(nonTransitive[0], nonTransitive[1]) < 0, compare(nonTransitive[1], nonTransitive[2]) < 0, compare(nonTransitive[2], nonTransitive[0]) < 0],
  note: 'Mixed NaN/unequal numeric priorities can form a non-transitive comparison. Their total sorted order is source-runtime-dependent, not a portable registry contract.' };
const actionKeys = new ActionHandlerRegistry();
const executed: string[] = [];
for (const kind of ['', '  ', '\t\n', '__proto__', '2', '10']) {
  actionKeys.register({ kind: kind as never, execute: () => { executed.push(`first:${kind}`); } });
  actionKeys.dispatch(null as never, { kind } as never);
  actionKeys.replace({ kind: kind as never, handsOffTurn: true, execute: () => { executed.push(`replaced:${kind}`); } });
  actionKeys.dispatch(null as never, { kind } as never);
}
actionKeys.replace({ kind: 'upsert' as never, execute: () => { executed.push('upsert'); } });
actionKeys.dispatch(null as never, { kind: 'upsert' } as never);
const actionKeyBoundaries = { keys: actionKeys.keys(), executed, handsOff: actionKeys.keys().map(kind => actionKeys.handsOffTurn({ kind } as never)) };
const metadata = new ActionHandlerRegistry();
const flags = (registry: ActionHandlerRegistry, kind: string) => ({ handsOff: registry.handsOffTurn({ kind } as never), duringDeployment: registry.duringDeployment({ kind } as never) });
metadata.register({ kind: 'custom' as never, execute: () => {}, handsOffTurn: true, duringDeployment: true });
const metadataClone = metadata.clone().seal();
metadata.replace({ kind: 'custom' as never, execute: () => {} });
metadata.replace({ kind: 'deployment' as never, execute: () => {}, duringDeployment: true });
const actionMetadata = { clone: flags(metadataClone, 'custom'), replaced: flags(metadata, 'custom'),
  introduced: flags(metadata, 'deployment'), unknown: flags(metadata, 'unknown'), cloneSealed: metadataClone.isSealed };
const result = { actionMetadata, sourceRuntime: { node: process.version, v8: process.versions.v8 }, actionKeyBoundaries, keyed: subjects.map(keyed), priority: priorityChecks(), numeric, nonFiniteCharacterization };
const output = JSON.stringify(result, null, 2);
writeFileSync(process.argv[2], process.argv[2].endsWith('.gz') ? gzipSync(output) : output);
console.log(`Source registry oracle: ${result.keyed.reduce((n, test) => n + test.checks.length, 0)} keyed checks, ${result.priority.length} priority checks, ${numeric.length} numeric boundaries; NaN comparator cycle characterized.`);
