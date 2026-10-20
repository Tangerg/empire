"""Run deliberate local source mutants; never edit the working source or Git index."""
from pathlib import Path
import os
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
source = root / 'godot/Presentation/Tactical'
project = root / 'tools/godot/tactical-selection-checks/TacticalSelectionChecks.csproj'
mutants = {
    'noncommandable-unit-paint': ('if (unit is null || queries.HumanTurn && unit.CanAct)', 'if (true)'),
    'selected-unit-ring-dropped': ('if (unit is not null) selected = unit.At;', ''),
    'enemy-inspection-threat-dropped': ('threat.AddRange(queries.Threat(inspectedId));', ''),
    'leave-clears-inspection': ('Cursor = null;\n        return Result();', 'Cursor = null;\n        InspectId = null;\n        return Result();'),
    'roster-pick-becomes-board-click': ('Selection = Pick(TacticalSelectionKind.DeploymentUnit, id);\n        InspectId = id;\n        return new TacticalInputResult(TacticalInputStatus.Ready, Reveal: unit.At, Center: true);', 'return Click(expected, unit.At);'),
    'surrogate-replacement-serializer': ('EcmaJson.ToElement(action)', 'JsonSerializer.SerializeToElement(action)'),
    'best-spot-last-tie': ('score <= best', 'score < best'),
    'configured-grid-last-tie': ('score <= reach', 'score < reach'),
    'stale-binding-accepted': ('if (expected.Binding != stamp.Binding || queries.Stamp.Binding != stamp.Binding) return TacticalInputStatus.StaleBinding;', ''),
    'helpful-painted-hostile': ('(selection.Hostile ? attack : helpful)', '(true ? attack : helpful)'),
    'command-key-precedence-lost': ('if (command is not null && Selection.Kind == TacticalSelectionKind.Destination)', 'if (command is not null && false)'),
}
with tempfile.TemporaryDirectory(prefix='empire-selection-mutants-') as tmp:
    directory = Path(tmp)
    oracle = directory / 'oracle.json'
    subprocess.run(['node', 'node_modules/vite-node/vite-node.mjs', 'tools/godot/tactical-selection-checks/export-oracle.ts', str(oracle)], cwd=root, check=True)
    for name, (before, after) in mutants.items():
        target = directory / name
        target.mkdir()
        for path in [source / 'ITacticalSelectionQueries.cs', source / 'TacticalSelection.cs']:
            text = path.read_text()
            if path.name == 'TacticalSelection.cs':
                assert before in text, name
                text = text.replace(before, after)
            (target / path.name).write_text(text)
        result = subprocess.run(['dotnet', 'run', '--project', str(project), f'-p:TacticalRoot={target}', '--', str(oracle)], cwd=root, capture_output=True, text=True)
        output = result.stdout + result.stderr
        if result.returncode == 0 or 'InvalidOperationException' not in output:
            raise RuntimeError(f'{name} did not fail a behavior assertion:\n{output}')
        print(f'Killed mutant: {name}')
