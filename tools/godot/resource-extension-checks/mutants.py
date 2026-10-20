#!/usr/bin/env python3
"""Deliberately break resource policies in disposable copies and require the intended assertion."""
from pathlib import Path
import shutil
import subprocess
import sys

root, scratch, oracle = map(Path, sys.argv[1:])
resource_mutants = [
    ('fractional-normalization', 'return adapter.Integer ? StateJson.Round(amount) : amount;',
     'return StateJson.Round(amount);', 'fuel credit 0.49 result'),
    ('skip-preflight', 'foreach (var cost in totals)\n            if (!CanSpend(subject, cost.Resource, cost.Value))',
     'foreach (var cost in totals.Take(0))\n            if (!CanSpend(subject, cost.Resource, cost.Value))', 'mixed spendAll trace'),
    ('planning-weight', '* Adapters.Get(StateJson.Text(amount, "resource")).AiWeight',
     '* 1', 'repeated value result'),
    ('holder-dispatch', 'Subjects.Get(subject.Kind).Ref(subject)',
     'Subjects.Get("player").Ref(subject)', 'weapon weapon_uses ref result'),
    ('resolver-original-transaction', 'var subject = SubjectFor(context, transaction);',
     'var subject = SubjectFor(context, transaction with { Amount = total.Amount });',
     'duplicate transaction aggregates but resolver sees original trace'),
    ('clone-isolation', 'new(Adapters.Clone(), Subjects.Clone())',
     'new(Adapters, Subjects)', 'resource clone isolation result'),
]
mutants = [(name, 'Resources', 'EmpireResourcesRoot', 'BattleResources.cs', before, after, expected)
           for name, before, after, expected in resource_mutants] + [
    ('resolved-readiness', 'Combat', 'EmpireCombatRoot', 'CombatForecasts.cs',
     'return resources.CanAffordTransactions(context, Transactions(weapon["resourceRequirements"]!.AsArray()))',
     'return new BattleResources().CanAffordTransactions(context, Transactions(weapon["resourceRequirements"]!.AsArray()))',
     'custom squad fractional ready result'),
    ('resolved-consumption', 'Combat', 'EmpireCombatRoot', 'CombatForecasts.cs',
     'var subject = resources.SubjectFor(context, cost);',
     'var subject = new BattleResources.Subject("player", context.Player!);',
     'custom squad fractional consume error'),
    ('injected-support-resource', 'Orders', 'EmpireOrdersRoot', 'SupportOrders.cs',
     'this.resources = resources;', 'this.resources = new BattleResources();',
     'supplied resource tactic tactic-options menus'),
]
for name, directory, property_name, filename, before, after, expected in mutants:
    mutant = scratch / name
    shutil.copytree(root / 'godot/Empire.Core' / directory, mutant)
    target = mutant / filename
    source = target.read_text()
    if source.count(before) != 1:
        raise SystemExit(f'{name}: mutation anchor must occur exactly once')
    target.write_text(source.replace(before, after))
    output = scratch / (name + '-bin')
    build = subprocess.run([
        'dotnet', 'build', str(scratch / 'ResourceExtensionChecks.csproj'), '--configfile', str(scratch / 'NuGet.Config'),
        '--disable-build-servers', '--nologo', '-v', 'quiet', f'-p:EmpireRoot={root}',
        f'-p:{property_name}={mutant}', f'-p:OutputPath={output}',
    ], capture_output=True, text=True)
    if build.returncode:
        raise SystemExit(f'{name}: invalid mutant\n{build.stdout}\n{build.stderr}')
    result = subprocess.run(['dotnet', str(output / 'ResourceExtensionChecks.dll'), str(oracle)], capture_output=True, text=True)
    (scratch / (name + '.log')).write_text(result.stdout + result.stderr)
    if result.returncode == 0 or f'System.Exception: {expected}:' not in result.stderr:
        raise SystemExit(f'{name}: missing intended assertion {expected}\n{result.stdout}\n{result.stderr}')
    print(f'PASS: {name} mutant rejected by {expected}', flush=True)
