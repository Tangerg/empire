#!/usr/bin/env python3
"""Compile each bypass, then require its semantic guard to fail for the named reason."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root, oracle = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()
suite = Path(__file__).resolve().parent
mutants = [
    ('condition-default-bypass', 'Scenario/ScenarioHandlers.cs',
     'Get(StateJson.Text(condition, "type")).Evaluate',
     'DefaultScenarioConditions.Create(Random).Get(StateJson.Text(condition, "type")).Evaluate',
     'unknown scenario condition handler "testGate"'),
    ('effect-default-bypass', 'Scenario/ScenarioHandlers.cs',
     'Get(StateJson.Text(effect, "type")).Apply',
     'DefaultScenarioEffects.Create().Get(StateJson.Text(effect, "type")).Apply',
     'unknown scenario effect handler "testAward"'),
    ('objective-default-bypass', 'Objectives/ObjectiveHandlers.cs',
     'Get(Text(objective, "type")).Outcome',
     'DefaultObjectives.Create().Get(Text(objective, "type")).Outcome',
     'unknown objective handler "testGoal"'),
    ('self-refresh-recurses', 'Lifecycle/VictorySystem.cs',
     'mode == "self" ? [] : Handlers.Children(objective)',
     'Handlers.Children(objective)', 'Mismatch: custom scenario events'),
    ('objective-role-bypass', 'Objectives/ObjectiveHandlers.cs',
     'Get(Text(objective, "type")).Role', '"primary"', 'Mismatch: handler-owned metadata'),
    ('health-ratio-unclamped', 'Scenario/DefaultScenarioConditions.cs',
     'Math.Clamp(Number(unit["hp"], "hp") / maximum, 0, 1)',
     '(Number(unit["hp"], "hp") / maximum)', 'Mismatch: unitHealth clamps'),
    ('zero-maximum-is-empty', 'Scenario/DefaultScenarioConditions.cs',
     '/ maximum, 0, 1) : 1', '/ maximum, 0, 1) : 0', 'Mismatch: unitHealth nonpositive maximum is whole'),
]
effect_file = root / 'godot/Empire.Core/Scenario/DefaultScenarioEffects.cs'
teleport = next(line for line in effect_file.read_text().splitlines() if '.Register(new("teleportUnits"' in line)
mutants.append(('destination-zone-order', 'Scenario/DefaultScenarioEffects.cs', teleport,
    teleport.replace('new PayloadReferences().Zone(Text(effect, "zone")).Selector(Object(effect["selector"], "selector"))',
        'new PayloadReferences().Selector(Object(effect["selector"], "selector")).Zone(Text(effect, "zone"))'),
    'Mismatch: default effect references'))
mutants.extend([
    ('explicit-null-is-absent', 'Objectives/DefaultObjectives.cs',
     '!objective.ContainsKey("minimumNeutralized")', 'objective["minimumNeutralized"] is null',
     'Mismatch: default objective references'),
    ('dotnet-whitespace-bypass', 'Scenario/DefaultScenarioEffects.cs',
     'EcmaText.IsBlank(Text(rule, "id"))', 'Text(rule, "id").Trim().Length == 0',
     'Mismatch: default effect references'),
])
work = Path(tempfile.mkdtemp(prefix='empire-scenario-mutants-', dir=os.environ.get('TMPDIR', '/tmp')))
print(f'Mutant artifacts: {work}', flush=True)
for name, filename, needle, change, expected in mutants:
    target = work / name
    shutil.copytree(root / 'godot/Empire.Core', target / 'godot/Empire.Core', ignore=shutil.ignore_patterns('bin', 'obj'))
    shutil.copytree(suite, target / 'tools/godot/scenario-extension-checks', ignore=shutil.ignore_patterns('bin', 'obj'))
    file = target / 'godot/Empire.Core' / filename
    text = file.read_text()
    if text.count(needle) != 1:
        raise RuntimeError(f'{name}: expected one explicit mutation target, found {text.count(needle)}')
    file.write_text(text.replace(needle, change))
    project = target / 'tools/godot/scenario-extension-checks/ScenarioExtensionChecks.csproj'
    result = subprocess.run(['dotnet', 'build', str(project), '--configfile', str(project.parent / 'NuGet.Config'), '--disable-build-servers', '--nologo', f'-p:EmpireRoot={target}'], capture_output=True, text=True)
    (target / 'build.log').write_text(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError(f'{name} failed to compile; that is not a killed mutant: {target / "build.log"}')
    run = subprocess.run(['dotnet', str(project.parent / 'bin/Debug/net8.0/ScenarioExtensionChecks.dll'), str(oracle)], capture_output=True, text=True)
    (target / 'run.log').write_text(run.stdout + run.stderr)
    if run.returncode == 0 or expected not in run.stdout + run.stderr:
        raise RuntimeError(f'{name} did not fail for intended semantic reason: {target / "run.log"}')
    print(f'PASS compiled mutant {name}: {expected}', flush=True)
print(f'PASS {len(mutants)} compiled bypass/edge mutants', flush=True)
