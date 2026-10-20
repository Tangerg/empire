import { writeFileSync } from 'node:fs';
import { SrpgMicrokernel, type EnginePlugin, type KernelPluginContext } from '../../../packages/battle-engine/src/kernel';
import { orderByDependencies } from '../../../packages/battle-engine/src/dependency-order';
import { anonymousOverrideId, createBattleRulesetManifest, requirePersistentRuleset, rulesetDifferences, type BattleRulesetIdentity } from '../../../packages/battle-engine/src/ruleset-manifest';

declare module '../../../packages/battle-engine/src/kernel' {
  interface KernelCapabilityMap {
    'test.a': string | null;
    'test.b': string | null;
    'test.c': string | null;
    'test.missing': string | null;
    'test.\uE000': string | null;
    'test.😀': string | null;
  }
}
type Key = 'test.a' | 'test.b' | 'test.c' | 'test.missing' | 'test.\uE000' | 'test.😀';
type Op = { op: 'provide' | 'replace' | 'require' | 'has' | 'provider'; key: Key; value?: string | null } |
  { op: 'manifest' | 'fail'; value?: string };
interface Plugin { id: string; version?: number | string; provides?: Key[]; overrides?: Key[]; requiresCapabilities?: Key[]; requires?: string[]; ops?: Op[] }
interface KernelCase { name: string; kind: 'kernel'; plugins: Plugin[]; queries?: Key[] }
interface Node { id: string; requires?: string[] }
interface OrderCase { name: string; kind: 'dependency'; nodes: Node[]; external?: string[] }
type Case = KernelCase | OrderCase;
const provide = (id = 'base', key: Key = 'test.a', value: string | null = 'original'): Plugin =>
  ({ id, provides: [key], ops: [{ op: 'provide', key, value }] });
const replace = (id = 'swap', key: Key = 'test.a', value = 'replacement'): Plugin =>
  ({ id, overrides: [key], ops: [{ op: 'replace', key, value }] });
const consume = (id = 'consumer', key: Key = 'test.a'): Plugin =>
  ({ id, requiresCapabilities: [key], ops: [{ op: 'require', key }] });
const k = (name: string, plugins: Plugin[], queries: Key[] = ['test.a', 'test.b', 'test.missing']): KernelCase =>
  ({ name, kind: 'kernel', plugins, queries });
const d = (name: string, nodes: Node[], external: string[] = []): OrderCase => ({ name, kind: 'dependency', nodes, external });
const cases: Case[] = [
  d('empty dependency graph', []),
  d('dependencies precede consumer without global sorting', [{ id: 'feature', requires: ['foundation'] }, { id: 'unrelated' }, { id: 'foundation' }]),
  d('external dependency satisfied', [{ id: 'addon', requires: ['installed'] }], ['installed']),
  d('batch node wins over external satisfaction', [{ id: 'child', requires: ['base'] }, { id: 'base' }], ['base']),
  d('missing dependency', [{ id: 'addon', requires: ['missing'] }]),
  d('duplicate dependency nodes', [{ id: 'same' }, { id: 'same' }]),
  d('self cycle', [{ id: 'self', requires: ['self'] }]),
  d('complete cycle slice', [{ id: 'entry', requires: ['left'] }, { id: 'left', requires: ['right'] }, { id: 'right', requires: ['left'] }]),
  d('diamond and duplicate dependency edges', [{ id: 'top', requires: ['right', 'left', 'right'] }, { id: 'left', requires: ['root'] }, { id: 'right', requires: ['root'] }, { id: 'root' }]),
  d('case-sensitive node identity', [{ id: 'A' }, { id: 'a' }]),
  k('empty kernel', []),
  k('open capabilities and insertion manifest', [provide('z', 'test.b'), provide('a')]),
  k('consumer installed after late provider', [consume(), { id: 'unrelated' }, provide()]),
  k('explicit dependencies preserve DFS order', [{ id: 'feature', requires: ['base'] }, { id: 'unrelated' }, provide()]),
  k('self explicit dependency is filtered', [{ id: 'self', requires: ['self'] }]),
  k('unused declared requirement is valid', [{ id: 'unused', requiresCapabilities: ['test.a'] }, provide()]),
  k('probes never consume or create edges', [{ id: 'probe-before', ops: [{ op: 'has', key: 'test.a' }, { op: 'provider', key: 'test.a' }] }, provide(), { id: 'probe-after', ops: [{ op: 'has', key: 'test.a' }, { op: 'provider', key: 'test.a' }, { op: 'has', key: 'test.missing' }] }]),
  k('override precedes all install-time consumers', [consume(), provide(), replace()]),
  k('override consumes previous value without self cycle', [consume(), { ...replace(), requiresCapabilities: ['test.a'], ops: [{ op: 'require', key: 'test.a' }, { op: 'replace', key: 'test.a', value: 'decorated' }] }, provide()]),
  k('self-provide consumption declared', [{ ...provide(), requiresCapabilities: ['test.a'], ops: [{ op: 'provide', key: 'test.a', value: 'self' }, { op: 'require', key: 'test.a' }] }]),
  k('self-provide consumption undeclared', [{ ...provide(), ops: [{ op: 'provide', key: 'test.a', value: 'self' }, { op: 'require', key: 'test.a' }] }]),
  k('undeclared consumer cannot capture outrun value', [provide(), { id: 'greedy', ops: [{ op: 'require', key: 'test.a' }] }, replace()]),
  k('declared missing capability fails before installation', [{ id: 'first' }, consume()]),
  k('undeclared missing require reports missing first', [{ id: 'greedy', ops: [{ op: 'require', key: 'test.a' }] }]),
  k('missing explicit plugin dependency', [{ id: 'child', requires: ['missing'] }]),
  k('plugin cycle retains path', [{ id: 'entry', requires: ['left'] }, { id: 'left', requires: ['right'] }, { id: 'right', requires: ['left'] }]),
  k('override missing introducer', [replace()]),
  k('competing declared introducers', [provide('first'), provide('second')]),
  k('duplicate actual provide', [provide(), { id: 'sneaky', ops: [{ op: 'provide', key: 'test.a', value: 'second' }] }]),
  k('duplicate actual provide within plugin', [{ ...provide(), ops: [{ op: 'provide', key: 'test.a', value: 'first' }, { op: 'provide', key: 'test.a', value: 'second' }] }]),
  k('competing override declarations', [provide(), replace('left'), replace('right')]),
  k('repeated override declaration itself ambiguous', [provide(), { ...replace(), overrides: ['test.a', 'test.a'] }]),
  k('repeated provide declaration is set-like', [{ ...provide(), provides: ['test.a', 'test.a'] }]),
  k('undeclared actual provide rejected', [{ id: 'sneaky', ops: [{ op: 'provide', key: 'test.a', value: 'value' }] }]),
  k('declared provide not installed', [{ id: 'idle', provides: ['test.a'] }]),
  k('declared missing checked before undeclared extra', [{ id: 'liar', provides: ['test.a'], ops: [{ op: 'provide', key: 'test.b', value: 'wrong' }] }]),
  k('undeclared actual override rejected', [provide(), { id: 'sneaky', requiresCapabilities: ['test.a'], ops: [{ op: 'replace', key: 'test.a', value: 'wrong' }] }]),
  k('declared override not installed', [provide(), { id: 'idle', overrides: ['test.a'] }]),
  k('undeclared replacement before introduction', [{ id: 'sneaky', ops: [{ op: 'replace', key: 'test.a', value: 'wrong' }] }]),
  k('multiple replacements within one plugin allowed', [provide(), { ...replace(), ops: [{ op: 'replace', key: 'test.a', value: 'first' }, { op: 'replace', key: 'test.a', value: 'last' }] }]),
  k('plugin may introduce and replace own slot', [{ ...provide(), overrides: ['test.a'], ops: [{ op: 'provide', key: 'test.a', value: 'first' }, { op: 'replace', key: 'test.a', value: 'last' }] }]),
  k('context manifest is empty while result carries identity', [{ ...provide(), version: 7, ops: [{ op: 'manifest' }, { op: 'provide', key: 'test.a', value: 'value' }] }]),
  k('null is a present capability', [provide('base', 'test.a', null), consume()]),
  k('installation errors propagate with prior side effects', [provide(), { id: 'throwing', ops: [{ op: 'fail', value: 'installation failed' }] }, { id: 'not-installed' }]),
  k('same id same version rejected', [{ id: 'same' }, { id: 'same' }]),
  k('same id different version rejected', [{ id: 'same', version: 2 }, { id: 'same', version: 3 }]),
  k('empty plugin id', [{ id: '' }]),
  k('blank plugin id', [{ id: ' \t\n' }]),
  k('ECMAScript BOM is whitespace', [{ id: '\uFEFF' }]),
  k('ECMAScript next-line is not whitespace', [{ id: '\u0085' }]),
  k('zero plugin version', [{ id: 'bad', version: 0 }]),
  k('negative plugin version', [{ id: 'bad', version: -1 }]),
  k('fractional plugin version', [{ id: 'bad', version: 1.5 }]),
  k('NaN plugin version', [{ id: 'bad', version: 'NaN' }]),
  k('infinite plugin version', [{ id: 'bad', version: 'Infinity' }]),
  k('negative infinite plugin version', [{ id: 'bad', version: '-Infinity' }]),
  k('plugin version beyond Int32', [{ id: 'wide', version: 2147483648 }]),
  k('plugin version beyond safe integer', [{ id: 'wide', version: 9007199254740992 }]),
  k('largest finite integer plugin version', [{ id: 'wide', version: Number.MAX_VALUE }]),
  k('duplicate wide version uses ECMAScript decimal formatting', [{ id: 'wide', version: 100000000000000000000 }, { id: 'wide', version: 1e21 }]),
  k('case sensitive plugin IDs', [{ id: 'A' }, { id: 'a' }]),
  k('combined override cycle is not silently reordered', [provide('base', 'test.a'), { id: 'middle', provides: ['test.b'], requiresCapabilities: ['test.a'], ops: [{ op: 'require', key: 'test.a' }, { op: 'provide', key: 'test.b', value: 'b' }] }, { id: 'combined', overrides: ['test.a', 'test.b'], ops: [{ op: 'replace', key: 'test.a', value: 'a2' }, { op: 'replace', key: 'test.b', value: 'b2' }] }]),
  k('separate overrides avoid composition cycle', [consume('end', 'test.b'), { id: 'middle', provides: ['test.b'], requiresCapabilities: ['test.a'], ops: [{ op: 'require', key: 'test.a' }, { op: 'provide', key: 'test.b', value: 'b' }] }, provide(), replace('swap-a'), replace('swap-b', 'test.b')]),
];
function message(error: unknown): string { return error instanceof Error ? error.message : String(error); }
function run(input: Case): unknown {
  if (input.kind === 'dependency') {
    const before = JSON.stringify(input.nodes);
    try {
      const ordered = orderByDependencies(input.nodes, {
        idOf: node => node.id,
        dependenciesOf: node => node.requires ?? [],
        isSatisfiedExternally: id => input.external?.includes(id) ?? false,
        duplicate: id => new Error(`duplicate dependency node: "${id}"`),
        missing: (node, id) => new Error(`${node.id} misses ${id}`),
        cycle: path => new Error(`cycle: ${path.join(' -> ')}`),
      });
      return { order: ordered.map(node => node.id), inputUnchanged: before === JSON.stringify(input.nodes) };
    } catch (error) { return { error: message(error), inputUnchanged: before === JSON.stringify(input.nodes) }; }
  }
  const trace: unknown[] = [];
  try {
    const kernel = new SrpgMicrokernel();
    for (const inputPlugin of input.plugins) {
      const plugin: EnginePlugin = {
        ...inputPlugin, version: Number(inputPlugin.version ?? 1),
        install(context) {
          trace.push(['install', inputPlugin.id]);
          for (const op of inputPlugin.ops ?? []) {
            if (op.op === 'provide') context.provide(op.key, op.value ?? null);
            else if (op.op === 'replace') context.replace(op.key, op.value ?? null);
            else if (op.op === 'require') trace.push(['require', op.key, context.require(op.key)]);
            else if (op.op === 'has') trace.push(['has', op.key, context.has(op.key)]);
            else if (op.op === 'provider') trace.push(['provider', op.key, context.providerOf(op.key) ?? null]);
            else if (op.op === 'manifest') trace.push(['manifest', [...context.pluginManifest]]);
            else if (op.op === 'fail') throw new Error(op.value);
          }
        },
      };
      kernel.use(plugin);
    }
    const result = kernel.compose();
    return { trace, manifest: [...result.pluginManifest], queries: (input.queries ?? []).map(id => ({ id, has: result.has(id), provider: result.providerOf(id) ?? null, value: result.has(id) ? result.require(id) : null })) };
  } catch (error) { return { trace, error: message(error) }; }
}
function lifecycle() {
  let calls = 0;
  const contexts: KernelPluginContext[] = [];
  const kernel = new SrpgMicrokernel().use({ id: 'base', version: 2, provides: ['test.a'], install(context) {
    calls++;
    contexts.push(context);
    context.provide('test.a', `value-${calls}`);
    if (calls === 1) throw new Error('first install fails');
  } });
  const before = [...kernel.pluginManifest];
  let failure = '';
  try { kernel.compose(); } catch (error) { failure = message(error); }
  const first = kernel.compose();
  const second = kernel.compose();
  contexts[1].replace('test.a', 'late replacement');
  contexts[1].provide('test.b', 'late addition');
  kernel.use({ id: 'later', version: 4, install() {} });
  const latest = kernel.compose();
  return {
    failure, calls, failedScopeValue: contexts[0].require('test.a'),
    first: [first.require('test.a'), first.providerOf('test.a'), first.require('test.b'), first.providerOf('test.b')],
    second: [second.require('test.a'), second.has('test.b')],
    before, firstManifest: [...first.pluginManifest], builderManifest: [...kernel.pluginManifest], latestManifest: [...latest.pluginManifest],
    scopeManifests: contexts.map(context => [...context.pluginManifest]),
  };
}
function persistence() {
  const identity = (plugins: ReadonlyMap<string, number>, packs: { id: string; version: number }[] = [{ id: 'z', version: 2 }, { id: 'a', version: 1 }]) =>
    createBattleRulesetManifest({ plugins, content: { packVersions: { all: () => packs } } as unknown as BattleRulesetIdentity['content'] });
  const kernel = new SrpgMicrokernel().use({ id: 'z.plugin', version: 3, install() {} }).use({ id: 'a.plugin', version: 2, install() {} });
  const manifest = identity(kernel.compose().pluginManifest);
  const different = identity(new Map([['z.plugin', 4], ['new.plugin', 1]]));
  const errors: string[] = [];
  for (const value of [identity(new Map(), []), identity(new Map([[anonymousOverrideId('test.a'), 1]])), manifest]) {
    try { requirePersistentRuleset(value); errors.push('accepted'); } catch (error) { errors.push(message(error)); }
  }
  return { manifest, differences: rulesetDifferences(manifest, different), errors, anonymousId: anonymousOverrideId('test.a') };
}
writeFileSync(process.argv[2], JSON.stringify({ source: ['packages/battle-engine/src/kernel.ts', 'packages/battle-engine/src/dependency-order.ts', 'packages/battle-engine/src/ruleset-manifest.ts'], cases: cases.map(input => ({ input, expected: run(input) })), lifecycle: lifecycle(), persistence: persistence() }, null, 2) + '\n');
