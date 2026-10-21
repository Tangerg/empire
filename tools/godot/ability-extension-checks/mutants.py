import pathlib
import shutil
import subprocess
import sys

root, scratch = map(pathlib.Path, sys.argv[1:])
mutants = [
    ('captured-actor', 'AbilityCommands.cs', 'TacticalCommands.FinishCommand(state, unit);',
     'TacticalCommands.FinishCommand(state, movement.UnitId);', 'custom ability replaces captured actor object: state'),
    ('weapon-expansion', 'AbilityCommands.cs', 'if (choices.Count == 0) Add(ability, query, null);',
     'if (choices.Count == 0 || ability.Id == "test-charge") Add(ability, query, null);', 'custom soldier all menus: menus'),
    ('weapon-tags', 'AbilityCommands.cs', 'BlockedAbilityStatus(query.Unit, tags)',
     'BlockedAbilityStatus(query.Unit, ability.Tags)', 'second weapon ability blocked by selected weapon tags: menus'),
    ('selected-weapon', 'AbilityCommands.cs', 'movement.Destination, movement.Moved, weapon);',
     'movement.Destination, movement.Moved, null);', 'custom charge selected soldier_sword: events'),
    ('engagement-kind', 'AbilityCommands.cs', 'query.At, target, kind)',
     'query.At, target, "attack")', 'no-attacks hostile-action player 1: menus'),
    ('fractional-priority', 'AbilityCommands.cs',
     'rules.Abilities.Get(left.Ability).Priority - rules.Abilities.Get(right.Ability).Priority;',
     '(int)rules.Abilities.Get(left.Ability).Priority - (int)rules.Abilities.Get(right.Ability).Priority;', 'custom soldier all menus: menus'),
    ('stable-equal-keys', 'AbilityCommands.cs', 'return options.OrderBy(option => option,',
     'return options.OrderByDescending(option => option.Name, StringComparer.Ordinal).OrderBy(option => option,', 'stable duplicate weapon option keys: menus'),
    ('movement-death', 'AbilityCommands.cs', 'if (surviving is null) return;',
     'if (surviving is null) { ability.Execute(rules, query, target, emit); return; }', 'death during movement suppresses custom effect: events'),
    ('legacy-attack-tags', 'DefaultAbilities.cs', 'rules.Combat.CanUseWeapon(query.State, query.Unit, query.Moved, query.WeaponId)',
     'rules.Combat.CanUseAttack(query.State, query.Unit, query.Moved, query.WeaponId)', 'replaced attack tags do not retain legacy hardcoded tag: menus'),
    ('target-validation', 'AbilityCommands.cs', 'if (!Targets(ability, query).Contains(target.Value)) throw new IllegalActionException(new ActionRefusal("ability.target_invalid", new() { ["abilityId"] = ability.Id }));',
     'if (target is null) throw new IllegalActionException(new ActionRefusal("ability.target_invalid", new() { ["abilityId"] = ability.Id }));', 'no-attacks attack player 1: events'),
]
for name, file, before, after, expected in mutants:
    target = scratch / ('mutant-' + name)
    shutil.copytree(root / 'godot/Empire.Core/Abilities', target)
    path = target / file
    text = path.read_text()
    if text.count(before) != 1:
        raise RuntimeError(f'Guard target must match exactly once: {name}')
    path.write_text(text.replace(before, after))
    build = subprocess.run(['dotnet', 'build', str(scratch / 'AbilityExtensionChecks.csproj'),
        '--configfile', str(scratch / 'NuGet.Config'), '--disable-build-servers', '--nologo',
        f'-p:EmpireRoot={root}', f'-p:EmpireAbilitiesRoot={target}', f'-p:OutputPath={target / "bin"}'],
        capture_output=True, text=True)
    (scratch / f'{name}-build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise RuntimeError(f'Mutant did not compile: {name}; inspect {name}-build.log')
    check = subprocess.run(['dotnet', str(target / 'bin/AbilityExtensionChecks.dll'), str(scratch / 'oracle.json')],
        capture_output=True, text=True)
    log = check.stdout + check.stderr
    (scratch / f'{name}-guard.log').write_text(log)
    if check.returncode == 0 or expected not in log:
        raise RuntimeError(f'Mutant missed its intended assertion: {name}; {log}')
    print(f'PASS deliberate semantic mutant: {name}', flush=True)
