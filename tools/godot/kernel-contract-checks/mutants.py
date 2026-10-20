#!/usr/bin/env python3
"""Prove semantic guards by mutating real source/native owners in disposable copies."""
from pathlib import Path
import json
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[3]
oracle = Path(sys.argv[1]).resolve()
evidence = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else Path(tempfile.mkdtemp(prefix='empire-kernel-mutants-'))
evidence.mkdir(parents=True, exist_ok=True)
expected = json.loads(oracle.read_text())
versions = Path(sys.argv[3]).resolve() if len(sys.argv) > 3 else root / "godot/tests/oracle/kernel-version-contracts.json"
source_mutants = [
    ('blank-plugin-id', 'kernel.ts', '!plugin.id.trim()', '!plugin.id'),
    ('dependency-order', 'dependency-order.ts', 'ordered.push(item);', 'ordered.unshift(item);'),
    ('external-dependency', 'dependency-order.ts', '!options.isSatisfiedExternally?.(dependency)', 'true'),
    ('cycle-slice', 'dependency-order.ts', 'path.slice(cycleStart)', 'path.slice(0)'),
    ('duplicate-node', 'dependency-order.ts', 'if (byId.has(id))', 'if (byId.has("__never__"))'),
    ('override-before-consumer', 'kernel.ts', '          ...(overriders.get(capability) ?? []),', ''),
    ('declared-consumption', 'kernel.ts', 'this.assertConsumption(plugin, context.consumed);', ''),
    ('provide-manifest', 'kernel.ts', "this.assertManifest(plugin, 'provide', plugin.provides, store.providedBy(plugin.id));", ''),
    ('override-manifest', 'kernel.ts', "this.assertManifest(plugin, 'override', plugin.overrides, store.overriddenBy(plugin.id));", ''),
    ('introducer-identity', 'kernel.ts', 'return this.providers.get(id);', 'return this.overriders.get(id) ?? this.providers.get(id);'),
    ('competing-overrides', 'kernel.ts', 'claimants.length > 1', 'claimants.length > 2'),
    ('probe-consumption', 'kernel.ts', 'override require<K extends KernelCapabilityId>', 'override has(id: KernelCapabilityId): boolean { this.consumed.add(id); return super.has(id); }\n  override require<K extends KernelCapabilityId>'),
    ('registration-manifest-order', 'kernel.ts', '[...this.plugins.values()].map', '[...this.plugins.values()].reverse().map'),
]
native_mutants = [
    ('ECMAScript-whitespace', '../Determinism/EcmaText.cs', 'return value.All(IsWhitespace);', 'return string.IsNullOrWhiteSpace(value);'),
    ('dependency-order', 'DependencyOrder.cs', 'return ordered.AsReadOnly();', 'ordered.Reverse(); return ordered.AsReadOnly();'),
    ('external-dependency', 'DependencyOrder.cs', 'options.IsSatisfiedExternally?.Invoke(dependency) != true', 'dependency is not null'),
    ('cycle-slice', 'DependencyOrder.cs', 'path.Skip(cycleStart).Append(id)', 'path.Append(id)'),
    ('duplicate-node', 'DependencyOrder.cs', 'if (!byId.TryAdd(id, item)) throw options.Duplicate(id);', 'byId[id] = item;'),
    ('override-before-consumer', 'SrpgMicrokernel.cs', '.Concat(overriders.GetValueOrDefault(key.Id) ?? [])', '.Concat(Array.Empty<string>())'),
    ('declared-consumption', 'SrpgMicrokernel.cs', 'AssertConsumption(plugin, context.Consumed);', ''),
    ('provide-manifest', 'SrpgMicrokernel.cs', 'AssertManifest(plugin, "provide", plugin.Provides, store.ProvidedBy(plugin.Id));', ''),
    ('override-manifest', 'SrpgMicrokernel.cs', 'AssertManifest(plugin, "override", plugin.Overrides, store.OverriddenBy(plugin.Id));', ''),
    ('introducer-identity', 'KernelCapabilityStore.cs', 'return providers.GetValueOrDefault(key.Id);', 'return overriders.GetValueOrDefault(key.Id) ?? providers.GetValueOrDefault(key.Id);'),
    ('competing-overrides', 'SrpgMicrokernel.cs', 'claimants.Count > 1', 'claimants.Count > 2'),
    ('stable-id-type-contract', 'KernelCapabilityStore.cs', 'type != key.ValueType', 'type == typeof(void)'),
    ('wide-version-narrowed', '../Persistence/RulesetManifest.cs', 'pair.Value != Math.Truncate(pair.Value) || pair.Value < 1', 'pair.Value != Math.Truncate(pair.Value) || pair.Value < 1 || pair.Value > int.MaxValue'),
    ('wide-version-formatting', '../Persistence/RulesetManifest.cs', 'EcmaNumber.Format(StateJson.Number(saved[id], id))', 'saved[id]!.ToJsonString()'),
    ('registration-manifest-order', 'SrpgMicrokernel.cs', 'return new KernelCapabilities(store, PluginManifest);', 'return new KernelCapabilities(store, ordered.ToDictionary(plugin => plugin.Id, plugin => plugin.Version, StringComparer.Ordinal));'),
]

def mutate(file, old, new):
    text = file.read_text()
    if text.count(old) != 1:
        raise SystemExit(f'{file.name}: expected one anchor, found {text.count(old)}')
    file.write_text(text.replace(old, new))
    return text

with tempfile.TemporaryDirectory(prefix='empire-kernel-source-') as temporary:
    target = Path(temporary)
    source = target / 'packages/battle-engine/src'
    source.mkdir(parents=True)
    for filename in ['kernel.ts', 'dependency-order.ts', 'ruleset-manifest.ts', 'id-order.ts', 'domain/errors.ts']:
        destination = source / filename
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / 'packages/battle-engine/src' / filename, destination)
    harness = target / 'tools/godot/kernel-contract-checks'
    harness.mkdir(parents=True)
    shutil.copy2(root / 'tools/godot/kernel-contract-checks/oracle.ts', harness / 'oracle.ts')
    for name, filename, old, new in source_mutants:
        file = source / filename
        original = mutate(file, old, new)
        output = evidence / f'source-{name}.json'
        run = subprocess.run(['node', str(root / 'node_modules/vite-node/vite-node.mjs'), str(harness / 'oracle.ts'), str(output)], cwd=root, text=True, capture_output=True)
        file.write_text(original)
        (evidence / f'source-{name}.log').write_text(run.stdout + run.stderr)
        if run.returncode:
            raise SystemExit(f'Invalid source mutant {name}: oracle did not finish\n{run.stdout}\n{run.stderr}')
        actual = json.loads(output.read_text())
        if actual == expected:
            raise SystemExit(f'SURVIVED source mutant: {name}')
        differences = [entry['input']['name'] for entry, mutant in zip(expected['cases'], actual['cases']) if entry != mutant]
        if expected['lifecycle'] != actual['lifecycle']: differences.append('scope lifecycle')
        if expected['persistence'] != actual['persistence']: differences.append('persistence identity')
        print(f'KILLED source {name}: {", ".join(differences)}', flush=True)

with tempfile.TemporaryDirectory(prefix='empire-kernel-native-') as temporary:
    target = Path(temporary)
    for folder in ['godot/Empire.Core', 'tools/godot/kernel-contract-checks']:
        shutil.copytree(root / folder, target / folder, ignore=shutil.ignore_patterns('bin', 'obj'))
    project = target / 'tools/godot/kernel-contract-checks/KernelContractChecks.csproj'
    for name, filename, old, new in native_mutants:
        file = target / 'godot/Empire.Core/Composition' / filename
        original = mutate(file, old, new)
        build = subprocess.run(['dotnet', 'build', str(project), '-v', 'quiet'], text=True, capture_output=True)
        (evidence / f'native-{name}-build.log').write_text(build.stdout + build.stderr)
        if build.returncode:
            raise SystemExit(f'Invalid native mutant {name}: compilation failed\n{build.stdout}\n{build.stderr}')
        run = subprocess.run(['dotnet', 'run', '--no-build', '--project', str(project), '--', str(oracle), str(versions), str(root)], text=True, capture_output=True)
        file.write_text(original)
        (evidence / f'native-{name}.log').write_text(run.stdout + run.stderr)
        if run.returncode == 0:
            raise SystemExit(f'SURVIVED native mutant: {name}')
        if 'Kernel contract failures:' not in run.stderr:
            raise SystemExit(f'Unexpected native mutant failure {name}:\n{run.stdout}\n{run.stderr}')
        failures = [line.split(': ', 1)[0] for line in run.stderr.splitlines() if line.startswith('FAIL ')]
        print(f'KILLED native {name}: {", ".join(failures)}', flush=True)
print(f'All {len(source_mutants)} source and {len(native_mutants)} compiled native mutation guards passed. Evidence: {evidence}')
