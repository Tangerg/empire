"""Guard the interface edges that a pure-core runtime cannot exercise in the Godot application."""
from pathlib import Path
import sys

root = Path(sys.argv[1])
failures = []
for folder in ['godot/Application', 'godot/Empire.Core/Ai', 'godot/Empire.Core/Abilities']:
    for path in (root / folder).glob('*.cs'):
        text = path.read_text()
        if 'new BattleVision(' in text or '.Movement.' in text:
            failures.append(str(path.relative_to(root)) + ': bypasses composed space')
text = (root / 'godot/Application/GrayFlagSession.Tactical.cs').read_text()
if 'session.engine.Space.ThreatTiles(state, actor, session.engine.Space.ComputeMoveField(state, actor))' not in text:
    failures.append('tactical threat query must forward the custom movement field, matching source GameSession.threatOf')
session = (root / 'godot/Application/GrayFlagSession.cs').read_text()
if 'engine.Space.IsUnitVisible(state, viewer, unit, seen)' not in session or 'VisibleUnitIds(state, viewer, seen)' not in session:
    failures.append('application visibility must use the explicit spatial predicate, with shared seen tiles for overlays')
if 'engine.Space.VisibleUnits(' in session:
    failures.append('application visibility must not bypass an overridden predicate through inherited VisibleUnits')
hud = (root / 'godot/Application/GrayFlagSession.Hud.cs').read_text()
if 'RuleGrid(state)' not in hud or 'engine.Space.Board(state).Grid' in hud:
    failures.append('HUD rule queries must consume resolved grids, not a spatial board override')
if failures:
    raise SystemExit('\n'.join(failures))
print('PASS spatial application/AI edges and source session threat-field forwarding')
