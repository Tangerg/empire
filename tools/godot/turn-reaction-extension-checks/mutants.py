"""Compile actual semantic violations and require their intended differential assertion."""
from pathlib import Path
import shutil
import subprocess
import sys

root, scratch, fixture = map(Path, sys.argv[1:])
mutants = [
    ('initial-policy-capture', 'Lifecycle/TurnOrderSystem.cs', 'return policy.Begin(state, new(rules.Content, emit));',
     'return Active(state).Begin(state, new(rules.Content, emit));', 'initial policy captured across changed state policy'),
    ('active-policy-source', 'Lifecycle/TurnOrderSystem.cs', 'rules.TurnOrders.Get(Text(Object(state["turnOrder"], "turnOrder"), "policy"))',
     'rules.TurnOrders.Get(Text(Object(state["rules"], "rules"), "turnOrder"))', 'initial policy captured across changed state policy'),
    ('phase-entitlement-gate', 'Lifecycle/TurnOrderSystem.cs', 'Text(state, "phase") == "playing" && CanAct(state, unit)',
     'CanAct(state, unit)', 'independent queries deployment policy queries'),
    ('delegated-actors', 'Lifecycle/TurnOrderSystem.cs', 'Active(state).Actors(state)',
     'Units(state).Where(unit => CanAct(state, unit)).ToArray()', 'independent queries playing policy queries'),
    ('round-before-exhaustion', 'Lifecycle/BattleLifecycle.cs', 'if (handoff.Exhausted)',
     'if (handoff.Exhausted && !handoff.RoundAdvanced)', 'exhaust custom concludes without round or claim'),
    ('reaction-registry-snapshot', 'Combat/CombatForecasts.cs', 'Reactions = reactions;',
     'Reactions = reactions.Clone();', 'forecast consumer snapshotted registry'),
    ('custom-interception', 'Combat/CombatForecasts.cs', 'Reaction(StateJson.Text(candidate, "reaction")).Intercepts',
     'StateJson.Text(candidate, "reaction") == "support"', 'custom interceptor takes damage not own multiplier complete forecast'),
    ('custom-resource-conservation', 'Combat/CombatForecasts.cs', 'stance.ConservesResources &&',
     'stance.Id == "conserve" &&', 'custom thrift rejects cooldown weapon complete forecast'),
    ('unknown-stance-prevalidation', 'Actions/DefaultActionHandlers.cs', 'unit["reaction"] = stance;',
     'context.Rules.Combat.Forecasts.Reaction(stance); unit["reaction"] = stance;', 'unknown stance action typed action failure'),
    ('support-identity', 'Combat/BattleCombat.Execution.cs', 'reaction.Stance == "support"',
     'Forecasts.Reaction(reaction.Stance).Intercepts', 'ward execution ordered events'),
]
for name, relative, before, after, expected in mutants:
    directory = scratch / ('mutant-' + name)
    core = directory / 'core'
    shutil.copytree(root / 'godot/Empire.Core', core, ignore=shutil.ignore_patterns('bin', 'obj'))
    path = core / relative
    source = path.read_text()
    if source.count(before) != 1:
        raise RuntimeError(f'{name}: guard target must match exactly once')
    path.write_text(source.replace(before, after))
    build = subprocess.run(['dotnet', 'build', str(scratch / 'TurnReactionExtensionChecks.csproj'),
        '--configfile', str(scratch / 'NuGet.Config'), '--disable-build-servers', '--nologo',
        f'-p:EmpireRoot={root}', f'-p:EmpireCoreRoot={core}', f'-p:OutputPath={directory / "bin"}'], capture_output=True, text=True)
    (scratch / f'{name}-build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise RuntimeError(f'{name}: mutant did not compile; inspect build log')
    run = subprocess.run(['dotnet', str(directory / 'bin/TurnReactionExtensionChecks.dll'), str(fixture)], capture_output=True, text=True)
    output = run.stdout + run.stderr
    (scratch / f'{name}-guard.log').write_text(output)
    if run.returncode == 0 or expected not in output:
        raise RuntimeError(f'{name}: mutant missed intended assertion {expected!r}: {output}')
    print(f'PASS compiled semantic mutant: {name}', flush=True)
