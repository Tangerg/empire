"""Run deliberate local source mutants; never edit the working source or Git index."""
from pathlib import Path
import os
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[3]
source = root / 'godot/Presentation/Tactical'
project = root / 'tools/godot/tactical-selection-checks/TacticalSelectionChecks.csproj'
mutants = {
    'cycle-retains-obsolete-cursor': ('CursorHit = TacticalHit.Unit(at, id);', ''),
    'invalid-target-discards-preview': ('return Result(TacticalInputStatus.InvalidTarget);', '{ Selection = Pick(TacticalSelectionKind.Idle); return Result(); }'),
    'sole-wait-commits-on-destination': ('// Choosing a destination is always a preview, even when waiting', 'var commands = queries.CommandsAt(unit.Id, at); if (commands.Count == 1 && commands[0].SelfTargeted) return Command(commands[0], unit.Id, path);\n                    // Choosing a destination is always a preview, even when waiting'),
    'outside-click-admitted': ('if (!InteractiveCells.Contains(at) || hit.Kind == TacticalHitKind.Unit && HitUnit(hit) is null)', 'if (hit.Kind == TacticalHitKind.Unit && HitUnit(hit) is null)'),
    'outside-hover-admitted': ('if (hit is { } candidate && !InteractiveCells.Contains(candidate.Cell))\n            return Leave(expected);', ''),
    'outside-keyboard-admitted': ('if (!InteractiveCells.Contains(to))\n                    continue;', ''),
    'noncommandable-unit-paint': ('if (unit is null || queries.HumanTurn && unit.CanAct)', 'if (true)'),
    'selected-unit-ring-dropped': ('if (unit is not null)\n                selected = unit.At;', ''),
    'enemy-inspection-threat-dropped': ('threat.AddRange(queries.Threat(inspectedId));', ''),
    'leave-clears-inspection': ('CursorHit = null;\n        return Result();', 'CursorHit = null;\n        InspectId = null;\n        return Result();'),
    'roster-pick-becomes-board-click': ('Selection = Pick(TacticalSelectionKind.DeploymentUnit, id);\n        InspectId = id;\n        CursorHit = TacticalHit.Unit(unit.At, id);\n        return new TacticalInputResult(TacticalInputStatus.Ready, Reveal: unit.At, Center: true, CursorFocused: true);', 'return Click(expected, TacticalHit.Unit(unit.At, unit.Id));'),
    'surrogate-replacement-serializer': ('EcmaJson.ToElement(action)', 'JsonSerializer.SerializeToElement(action)'),
    'best-spot-last-tie': ('score <= best', 'score < best'),
    'configured-grid-last-tie': ('score <= reach', 'score < reach'),
    'stale-binding-accepted': ('if (expected.Binding != stamp.Binding || queries.Stamp.Binding != stamp.Binding)\n            return TacticalInputStatus.StaleBinding;', ''),
    'helpful-painted-hostile': ('(selection.Hostile ? attack : helpful)', '(true ? attack : helpful)'),
    'command-key-precedence-lost': ('if (command is not null && Selection.Kind == TacticalSelectionKind.Destination)', 'if (command is not null && false)'),
    'ground-promoted-to-hidden-unit': ('hit is { Kind: TacticalHitKind.Unit, UnitId: { } id }\n        && queries.Unit(id) is { Visible: true } unit && unit.At == hit.Cell ? unit : null', 'hit is not null ? queries.UnitAt(hit.Cell) : null'),
    'typed-target-collapsed-to-cell': ('selection.TargetSubjects.Any(target => target.Accepts(hit))', 'selection.Targets.Contains(hit.Cell)'),
    'deployment-swap-collapsed-to-cell': ('queries.DeploymentTargets(unit.Id).Any(target => target.Accepts(hit))', 'queries.DeploymentTargets(unit.Id).Any(target => target.Cell == hit.Cell)'),
    'enter-reinfers-pointer-subject': ('CursorHit is { } hit ? Click(expected, hit)', 'CursorHit is { } hit ? Click(expected, KeyboardFocus(hit.Cell))'),
}
with tempfile.TemporaryDirectory(prefix='empire-selection-mutants-') as tmp:
    directory = Path(tmp)
    if len(sys.argv) > 1:
        oracle = Path(sys.argv[1]).resolve()
    else:
        import gzip
        oracle = directory / 'oracle.json'
        oracle.write_bytes(gzip.decompress((root / 'godot/tests/oracle/tactical-selection.json.gz').read_bytes()))
    for name, (before, after) in mutants.items():
        target = directory / name
        target.mkdir()
        for path in [source / name for name in ['TacticalHit.cs', 'TacticalTarget.cs', 'ITacticalSelectionQueries.cs', 'TacticalSelection.cs']]:
            text = path.read_text()
            if path.name == 'TacticalSelection.cs':
                assert before in text, name
                text = text.replace(before, after)
            (target / path.name).write_text(text)
        result = subprocess.run(['dotnet', 'run', '--disable-build-servers', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', '--project', str(project), f'-p:TacticalRoot={target}', '--', str(oracle)], cwd=root, capture_output=True, text=True)
        output = result.stdout + result.stderr
        if result.returncode == 0 or 'InvalidOperationException' not in output:
            raise RuntimeError(f'{name} did not fail a behavior assertion:\n{output}')
        print(f'Killed mutant: {name}')
