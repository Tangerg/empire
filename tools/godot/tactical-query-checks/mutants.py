#!/usr/bin/env python3
"""Run real native query mutants in disposable copies; never edit the shared tree."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[3]
oracle = Path(sys.argv[1]).resolve()
mutants = [
    ('drop unaffordable cards', 'Orders/RecruitmentOrders.cs', '        }).ToArray();', '        }).Where(card => card.Affordable).ToArray();'),
    ('aggregate card costs', 'Orders/RecruitmentOrders.cs', 'costs.All(cost => resources.CanSpend(subject, cost.Resource, cost.Amount))', 'resources.CanAfford(subject, StateJson.Array(content.Get("units", type)["recruitCosts"], "recruitCosts"))'),
    ('leak hidden objectives', 'Lifecycle/VictorySystem.Progress.cs', '.Where(objective => !IsTrue(Player(state, owner)["objectiveStates"]?[Text(objective, "id")]?["hidden"]))', '.Where(objective => true)'),
    ('change rank threshold', 'Combat/CombatEffects.cs', 'RankThresholds = [120, 320]', 'RankThresholds = [121, 320]'),
    ('invert account ordering', 'Resources/BattleResources.cs', 'SupportedResources.Where(resource => HasAccount(subject, resource))', 'SupportedResources.Reverse().Where(resource => HasAccount(subject, resource))'),
    ('neutral aura loses identity', 'Tactics/TacticalModifiers.cs', 'return grid.Distance(TacticalQueries.Position(unit), TacticalQueries.Position(leader)) <=', 'return StateJson.Number(commander["aura"]!["attackMultiplier"], "attack") != 1 && grid.Distance(TacticalQueries.Position(unit), TacticalQueries.Position(leader)) <='),
    ('first cast wins overlap', 'Combat/BattleCombat.Telegraphs.cs', 'result[cell] = cast.Remaining;', 'result.TryAdd(cell, cast.Remaining);'),
    ('flatten cast progress', 'Combat/BattleCombat.Telegraphs.cs', 'Math.Clamp((now - declared) / (resolve - declared), 0, 1)', '0'),
]
for name, relative, old, new in mutants:
    with tempfile.TemporaryDirectory(prefix='empire-query-mutant-') as work:
        target = Path(work)
        core = target / 'godot/Empire.Core'
        shutil.copytree(root / 'godot/Empire.Core', core, ignore=shutil.ignore_patterns('bin', 'obj'))
        harness = target / 'tools/godot/tactical-query-checks'
        shutil.copytree(root / 'tools/godot/tactical-query-checks', harness, ignore=shutil.ignore_patterns('bin', 'obj'))
        file = core / relative
        source = file.read_text()
        if source.count(old) != 1:
            raise SystemExit(f'{name}: mutation anchor count is {source.count(old)}, expected 1')
        file.write_text(source.replace(old, new))
        build = subprocess.run(['dotnet', 'build', str(harness), '-v', 'quiet'], capture_output=True, text=True)
        if build.returncode:
            raise SystemExit(f'{name}: invalid mutant, build failed\n{build.stdout}\n{build.stderr}')
        run = subprocess.run(['dotnet', 'run', '--no-build', '--project', str(harness), '--', str(oracle)], capture_output=True, text=True)
        if run.returncode == 0:
            raise SystemExit(f'{name}: SURVIVED')
        if 'Unhandled exception. System.Exception:' not in run.stderr:
            raise SystemExit(f'{name}: unexpected failure\n{run.stdout}\n{run.stderr}')
        print(f'KILLED {name}', flush=True)
