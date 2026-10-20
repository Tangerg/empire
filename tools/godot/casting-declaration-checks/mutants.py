"""Compile isolated semantic regressions and require their specific parity assertion to fail."""
import pathlib
import shutil
import subprocess
import sys

root, scratch = map(pathlib.Path, sys.argv[1:])
casting = 'Combat/BattleCombat.Casting.cs'
references = 'Persistence/BattlePersistence.References.cs'
shape = 'Persistence/BattlePersistence.Shape.cs'
mutants = [
    ('metadata-stamped-attack', casting, '["ability"] = ability', '["ability"] = "attack"',
     'metadata "heal" declaration and persisted resolution: event-hashes'),
    ('instant-weapon-accepted', casting, 'if (castTurns <= 0)', 'if (castTurns <= -999)', 'duration -3: error'),
    ('duplicate-accepted', casting, 'if (casts.OfType<JsonObject>().Any(value => StateJson.Integer(value, "caster") == id))',
     'if (casts.Count < 0)', 'duplicate cast is illegal and unchanged: error'),
    ('wrong-error-precedence', casting, 'if (castTurns <= 0)',
     'if (castTurns <= 0 && !StateJson.Objects(state["pendingCasts"], "pendingCasts").Any(value => StateJson.Integer(value, "caster") == StateJson.Integer(caster, "id")))',
     'instant weapon defect precedes duplicate refusal: error'),
    ('floor-duration', casting, 'StateJson.Round(castTurns)', 'Math.Floor(castTurns)', 'duration 1.5: events'),
    ('detached-return', casting, 'return cast;', 'return cast.DeepClone().AsObject();',
     'metadata "attack" declaration and persisted resolution: stored-identity'),
    ('closed-ability-admission', references, '        if (state["deployment"] is not null',
     '        foreach (var cast in StateJson.Objects(state["pendingCasts"], "pendingCasts"))\n'
     '            Reference("abilities", "能力", StateJson.Text(cast, "ability"), "咏唱");\n'
     '        if (state["deployment"] is not null',
     'metadata "test.unregistered" declaration and persisted resolution: error'),
    ('closed-weapon-shape-admission', shape,
     '            if (StateJson.Number(pending["resolveAt"], "resolveAt")',
     '            RequireContent("weapons", StateJson.Text(pending, "weapon"), "咏唱");\n'
     '            if (StateJson.Number(pending["resolveAt"], "resolveAt")',
     'unknown weapon survives save load then cancels: error'),
    ('closed-weapon-reference-admission', references, '        if (state["deployment"] is not null',
     '        foreach (var cast in StateJson.Objects(state["pendingCasts"], "pendingCasts"))\n'
     '            Weapon(StateJson.Text(cast, "weapon"), "咏唱");\n'
     '        if (state["deployment"] is not null', 'unknown weapon survives save load then cancels: error'),
    ('metadata-controls-resolution', casting,
     '            ExecutePlan(state, Forecast(state, caster, target, origin, weaponId), emit);',
     '            if (StateJson.Text(cast, "ability") == "attack") ExecutePlan(state, Forecast(state, caster, target, origin, weaponId), emit);',
     'metadata "heal" declaration and persisted resolution: events'),
    ('lost-caster-reason', casting, 'Cancel("casterLost")', 'Cancel("weaponUnavailable")',
     'orphaned caster cancellation: events'),
    ('live-origin-used', casting, '            var weapon = Forecasts.ReadyWeapon(state, caster, weaponId);',
     '            origin = TacticalQueries.Position(caster);\n            var weapon = Forecasts.ReadyWeapon(state, caster, weaponId);',
     'frozen engagement geometry 1,3: events'),
    ('reverse-due-order', casting, 'due.OrderBy(value =>', 'due.OrderByDescending(value =>',
     'due casts resolve oldest first even when stored reversed: events'),
]
for name, file, before, after, expected in mutants:
    target = scratch / ('mutant-' + name)
    core = target / 'Core'
    shutil.copytree(root / 'godot/Empire.Core', core, ignore=shutil.ignore_patterns('obj', 'bin', '*.uid'))
    path = core / file
    text = path.read_text()
    if text.count(before) != 1:
        raise RuntimeError(f'Guard target must match exactly once: {name}')
    path.write_text(text.replace(before, after))
    build = subprocess.run(['dotnet', 'build', str(scratch / 'CastingDeclarationChecks.csproj'),
        '--configfile', str(scratch / 'NuGet.Config'), '--disable-build-servers', '--nologo',
        f'-p:EmpireRoot={root}', f'-p:EmpireCoreRoot={core}', f'-p:OutputPath={target / "bin"}'],
        capture_output=True, text=True)
    (target / 'build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise RuntimeError(f'Mutant did not compile: {name}; inspect {target / "build.log"}')
    check = subprocess.run(['dotnet', str(target / 'bin/CastingDeclarationChecks.dll'), str(scratch / 'oracle.json')],
        capture_output=True, text=True)
    log = check.stdout + check.stderr
    (target / 'guard.log').write_text(log)
    if check.returncode == 0 or 'Mismatch in ' + expected not in log:
        raise RuntimeError(f'Mutant missed its intended assertion: {name}; {log}')
    print(f'PASS deliberate semantic mutant: {name}', flush=True)
