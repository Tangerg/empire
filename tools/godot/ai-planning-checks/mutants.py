import pathlib
import shutil
import subprocess
import sys

root, scratch = map(pathlib.Path, sys.argv[1:])
mutants = [
    ('evaluator-bypass', 'DefaultAiIntents.cs', 'context.AbilityEvaluators.TryGet(option.Ability)',
     'DefaultAbilityAiEvaluators.Create().TryGet(option.Ability)', 'registered ability and evaluator', 'action'),
    ('ability-whitelist', 'DefaultAiIntents.cs', 'context.Rules.Commands.Options(context.State, unit, at)',
     'context.Rules.Commands.Options(context.State, unit, at).Where(option => option.Ability is "attack" or "heal" or "capture" or "wait")', 'registered ability and evaluator', 'action'),
    ('movement-stops-bypass', 'DefaultAiIntents.cs', 'foreach (var stop in field.Stops)',
     'foreach (var stop in field.Tiles.Values.Where(tile => tile.Free).Select(tile => tile.Index))', 'space move field restricts stop', 'action'),
    ('space-path-coupling', 'DefaultAiIntents.cs', 'field.TracePath(stop)',
     'context.Rules.Space.PathTo(field, context.State, at)', 'space path query is independent of AI field trace', 'action'),
    ('space-board-coupling', 'AiTurnContext.cs', 'new TacticalBoard(Object(State["map"], "map"), Rules.Grids.Get(Text(State["rules"]!["grid"], "grid")))',
     'Rules.Space.Board(State)', 'space board query is independent of AI geometry', 'AI must resolve grids, not space.board'),
    ('threat-bypass', 'AiTurnContext.cs', 'context.Rules.Space.ThreatTiles(context.State, foe)',
     'System.Array.Empty<int>()', 'space threat replacement reaches appraisal', 'threat'),
    ('resource-weight-bypass', 'DefaultAiIntents.cs', 'context.Rules.Resources.PlanningValue(Array(tactic["costs"], "costs"))',
     'new BattleResources().PlanningValue(Array(tactic["costs"], "costs"))', 'resource AI weight suppresses tactic', 'action'),
    ('resource-account-bypass', 'DefaultAiIntents.cs', 'context.Rules.Resources.CanAfford(account,',
     'new BattleResources().CanAfford(account,', 'resolved resource account rejects recruitment', 'action'),
    ('turn-actors-bypass', 'AiTurnContext.cs', 'Rules.Turns.Actors(State).Where(unit => !IsTrue(unit["done"])).ToArray()',
     'Objects(State["units"], "units").ToArray()', 'finished actors omitted', 'action'),
]
for name, file, before, after, case, assertion in mutants:
    target = scratch / ('mutant-' + name)
    shutil.copytree(root / 'godot/Empire.Core/Ai', target)
    path = target / file
    text = path.read_text()
    if text.count(before) != 1:
        raise RuntimeError(f'Guard target must match exactly once: {name}')
    path.write_text(text.replace(before, after))
    build = subprocess.run(['dotnet', 'build', str(scratch / 'AiPlanningChecks.csproj'),
        '--configfile', str(scratch / 'NuGet.Config'), '--disable-build-servers', '--nologo',
        f'-p:EmpireRoot={root}', f'-p:EmpireAiRoot={target}', f'-p:OutputPath={target / "bin"}'], capture_output=True, text=True)
    (scratch / f'{name}-build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise RuntimeError(f'Mutant did not compile: {name}; inspect {name}-build.log')
    check = subprocess.run(['dotnet', str(target / 'bin/AiPlanningChecks.dll'), str(scratch / 'oracle.json'), case], capture_output=True, text=True)
    log = check.stdout + check.stderr
    (scratch / f'{name}-guard.log').write_text(log)
    expected = assertion if assertion.startswith('AI must') else f'Mismatch in {case}: {assertion}'
    if check.returncode == 0 or expected not in log:
        raise RuntimeError(f'Mutant missed intended behavioral assertion: {name}; {log}')
    print(f'PASS deliberate semantic mutant: {name}', flush=True)
