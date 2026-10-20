#!/usr/bin/env python3
"""Break isolated source copies to prove the native registry contract guards."""
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
work = Path(tempfile.mkdtemp(prefix="empire-registry-mutants."))
core = work / "godot/Empire.Core"
harness = work / "tools/godot/registry-checks"
shutil.copytree(root / "godot/Empire.Core", core, ignore=shutil.ignore_patterns("bin", "obj"))
shutil.copytree(root / "tools/godot/registry-checks", harness, ignore=shutil.ignore_patterns("bin", "obj"))
oracle = work / "oracle.json"
subprocess.run(["node", "node_modules/vite-node/vite-node.mjs", "tools/godot/registry-checks/oracle.ts", str(oracle)], cwd=root, check=True)
command = ["dotnet", "run", "--project", str(harness / "RegistryChecks.csproj"), "--", str(oracle)]
baseline = subprocess.run(command, cwd=work, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
(work / "baseline.log").write_text(baseline.stdout)
if baseline.returncode:
    raise SystemExit(f"Isolated baseline failed: {work}/baseline.log")
mutations = [
    ("duplicate-rejection", "Registries/KeyedRegistry.cs", r'throw new DomainInvariantException($"{subject} already registered: \"{key}\"");', "return (TRegistry)this;"),
    ("detached-keys", "Registries/KeyedRegistry.cs", "Keys() => order.ToArray();", "Keys() => order;"),
    ("stable-comparator-ties", "Registries/PriorityRegistry.cs", "Comparer<TEntry>.Create(Compare)).ToArray()", "Comparer<TEntry>.Create(Compare)).ThenByDescending(entry => Array.IndexOf(All().ToArray(), entry)).ToArray()"),
    ("upsert-not-replace-only", "Registries/KeyedRegistry.cs", "if (!entries.ContainsKey(key)) order.Add(key);", 'if (!entries.ContainsKey(key)) throw new DomainInvariantException("replace requires incumbent");'),
    ("replace-keeps-position", "Registries/KeyedRegistry.cs", "if (!entries.ContainsKey(key)) order.Add(key);", "order.Remove(key); order.Add(key);"),
    ("seal-blocks-writes", "Registries/KeyedRegistry.cs", "IsSealed = true;", "IsSealed = false;"),
    ("clone-is-mutable", "Registries/KeyedRegistry.cs", "return target;", "return target.Seal();"),
    ("ordinal-not-culture", "Registries/PriorityRegistry.cs", "StringComparer.Ordinal.Compare", "StringComparer.InvariantCulture.Compare"),
    ("fractional-priority", "Registries/PriorityRegistry.cs", "left.Priority - right.Priority", "(int)left.Priority - (int)right.Priority"),
    ("priority-cache-invalidation", "Registries/PriorityRegistry.cs", "orderedCache = null;", "// intentionally retain stale cache"),
    ("blank-action-key-admitted", "Transactions/BattleActionRegistry.cs", "protected override string KeyOf(BattleActionRegistration entry) => entry.Kind;", 'protected override string KeyOf(BattleActionRegistration entry) { ArgumentException.ThrowIfNullOrWhiteSpace(entry.Kind); return entry.Kind; }'),
]
for name, relative, before, after in mutations:
    path = core / relative
    original = path.read_text()
    if before not in original:
        raise SystemExit(f"Mutation anchor missing: {name}")
    path.write_text(original.replace(before, after))
    try:
        result = subprocess.run(command, cwd=work, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        (work / f"{name}.log").write_text(result.stdout)
        if result.returncode == 0:
            raise SystemExit(f"Mutant survived: {name}; {work}")
        if "error CS" in result.stdout or "build failed" in result.stdout.lower():
            raise SystemExit(f"Mutant did not compile: {name}; {work}")
        print(f"REJECTED {name}")
    finally:
        path.write_text(original)
path = core / "Transactions/Battle.cs"
original = path.read_text()
mutant = original.replace("IReadOnlyDictionary<string, BattleActionRegistration> _handlers", "BattleActionRegistry _handlers")
mutant = mutant.replace("_handlers = registry.Freeze();", "_handlers = registry;")
mutant = mutant.replace("if (!_handlers.TryGetValue(kind, out var registration) && pipeline is null)", "var registration = _handlers.TryGet(kind); if (registration is null && pipeline is null)")
if mutant == original:
    raise SystemExit("Battle isolation mutation anchor missing")
path.write_text(mutant)
result = subprocess.run(command, cwd=work, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
(work / "battle-snapshot-isolation.log").write_text(result.stdout)
if result.returncode == 0 or "error CS" in result.stdout or "build failed" in result.stdout.lower():
    raise SystemExit(f"Battle snapshot mutation failed to prove guard: {work}")
print("REJECTED battle-snapshot-isolation")
print(f"PASS {len(mutations) + 1} compiled source mutants rejected. Diagnostics: {work}")
