from pathlib import Path
import os
import shutil
import subprocess
import sys

root, fixtures, artifacts = map(Path, sys.argv[1:])
project = artifacts / 'SpatialExtensionChecks.csproj'
source = root / 'tools/godot/spatial-extension-checks'
mutants = [
    ('threat-virtual-sibling', 'Tactics/DefaultTacticalSpace.cs', 'movement.ThreatTiles(state, unit, field)', 'movement.ThreatTiles(state, unit, field ?? ComputeMoveField(state, unit))'),
    ('visibility-virtual-siblings', 'Tactics/DefaultTacticalSpace.cs', 'vision.VisibleUnits(state, viewer)', 'StateJson.Objects(state["units"], "units").Where(unit => IsUnitVisible(state, viewer, unit, VisibleTiles(state, viewer))).ToArray()'),
    ('discard-canonical-path', 'Tactics/TacticalCommands.cs', 'return new(unitId, origin, destination, canonical);', 'return new(unitId, origin, destination, new[] { origin, destination });'),
    ('bypass-target-policy', 'Abilities/AbilityCommands.cs', 'var targets = ability.Targets(rules, query);', 'IReadOnlyList<Coord> targets = new[] { new Coord(2, 1) };'),
    ('copy-move-field-identity', 'Tactics/MoveField.cs', 'Tiles = tiles;', 'Tiles = new Dictionary<int, ReachableTile>(tiles);'),
    ('recompute-custom-stops', 'Tactics/MoveField.cs', 'Stops = stops ?? tiles.Values', 'Stops = tiles.Values'),
    ('snapshot-resolved-grids', 'Tactics/DefaultTacticalSpace.cs', 'new MovementRules(content, grids)', 'new MovementRules(content, grids.Clone())'),
]
for name, relative, old, new in mutants:
    mutated = artifacts / name
    shutil.copytree(root / 'godot/Empire.Core', mutated / 'godot/Empire.Core', ignore=shutil.ignore_patterns('bin', 'obj'))
    path = mutated / 'godot/Empire.Core' / relative
    text = path.read_text()
    if text.count(old) != 1:
        raise RuntimeError(f'{name}: expected one mutation anchor, found {text.count(old)}')
    path.write_text(text.replace(old, new))
    build = subprocess.run(['dotnet', 'build', str(project), '--configfile', str(artifacts / 'NuGet.Config'), '--disable-build-servers', '--nologo', f'-p:EmpireRoot={mutated}'], capture_output=True, text=True)
    (artifacts / f'{name}.build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise RuntimeError(f'{name}: did not compile; mutation rejection is not semantic')
    run = subprocess.run(['dotnet', str(artifacts / 'bin/Debug/net8.0/SpatialExtensionChecks.dll'), str(fixtures / 'godot/tests/oracle/tactical-queries.json.gz'), str(fixtures / 'godot/tests/oracle/movement-vectors.json'), str(root / 'godot/tests/oracle/spatial-visibility.json.gz')], capture_output=True, text=True)
    (artifacts / f'{name}.run.log').write_text(run.stdout + run.stderr)
    if run.returncode == 0:
        raise RuntimeError(f'{name}: invalid mutant passed')
    print(f'PASS rejected {name}', flush=True)
application = artifacts / 'application-bypass'
shutil.copytree(root / 'godot/Application', application / 'godot/Application')
shutil.copytree(root / 'godot/Empire.Core', application / 'godot/Empire.Core', ignore=shutil.ignore_patterns('bin', 'obj'))
path = application / 'godot/Application/GrayFlagSession.Tactical.cs'
text = path.read_text().replace('session.engine.Space.ThreatTiles(state, actor, session.engine.Space.ComputeMoveField(state, actor))', 'session.engine.Space.ThreatTiles(state, actor)')
path.write_text(text)
run = subprocess.run([sys.executable, str(source / 'architecture.py'), str(application)], capture_output=True, text=True)
if run.returncode == 0:
    raise RuntimeError('application-field-bypass guard was vacuous')
print('PASS rejected application-field-bypass', flush=True)

path = application / 'godot/Application/GrayFlagSession.Tactical.cs'
path.write_text((root / 'godot/Application/GrayFlagSession.Tactical.cs').read_text())
path = application / 'godot/Application/GrayFlagSession.cs'
text = path.read_text().replace('engine.Space.IsUnitVisible(state, viewer, unit, seen)', 'true')
path.write_text(text)
run = subprocess.run([sys.executable, str(source / 'architecture.py'), str(application)], capture_output=True, text=True)
if run.returncode == 0:
    raise RuntimeError('application-visibility-bypass guard was vacuous')
print('PASS rejected application-visibility-bypass', flush=True)
path.write_text((root / 'godot/Application/GrayFlagSession.cs').read_text())
path = application / 'godot/Application/GrayFlagSession.Hud.cs'
path.write_text(path.read_text().replace('RuleGrid(state)', 'engine.Space.Board(state).Grid'))
run = subprocess.run([sys.executable, str(source / 'architecture.py'), str(application)], capture_output=True, text=True)
if run.returncode == 0:
    raise RuntimeError('hud-captured-grid-bypass guard was vacuous')
print('PASS rejected hud-captured-grid-bypass', flush=True)
