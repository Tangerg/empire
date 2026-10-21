"""Behavior-only native mutants; source working tree and index remain unchanged."""
from pathlib import Path
import os
import shutil
import subprocess
import tempfile

root = Path.cwd()
source = root / 'godot'
mutants = {
    'straightened-cubic-policy': ('BoardDecorations.cs', ' / 6d', ' / 5d', 'Actual source cubic path'),
    'missing-filter-spill': ('BoardDecorations.cs', 'var filtered = ShadowRaster.Filter(source, new[] { drop.ToDrop() }, density);', 'var filtered = (Image: (Image)source.Duplicate(), Padding: 0);', 'Action filter has source spill'),
    'ring-opacity-order': ('BoardDecorations.cs', 'return new(ImageTexture.CreateFromImage(filtered.Image), bounds.Grow(padding), opacity);', 'return new(ImageTexture.CreateFromImage(filtered.Image), bounds.Grow(padding), 1);', 'Selection opacity is post-filter'),
    'replaced-path-leaks': ('BoardDecorations.cs', 'pathDrawing?.Texture.Dispose();\n        pathDrawing = null;', 'pathDrawing = null;', 'Replaced path texture is disposed'),
    'ranges-over-actors': ('BoardOverlay.cs', 'ZIndex = -2 }', 'ZIndex = 3000 }', 'Source ranges/path below actors'),
    'outside-spots': ('BoardOverlay.cs', '_playable?.Contains(cell) ?? true', 'true', 'Source range ordering'),
    'missing-attack-exclusion': ('BoardOverlay.cs', ' && !tactical.Attack.Contains(cell)', '', 'Source range ordering'),
    'route-no-bleed': ('RouteComposition.cs', 'local.X >= 32.5f', 'local.X >= 32f', 'Per-cell source mask bleeds'),
}
with tempfile.TemporaryDirectory(prefix='empire-decoration-mutants-') as temporary:
    project = Path(temporary) / 'godot'
    shutil.copytree(source, project, ignore=shutil.ignore_patterns('.godot', 'bin', 'obj', 'assets', 'data', 'tests'))
    for directory in ['assets', 'data', 'tests']:
        (project / directory).symlink_to(source / directory, target_is_directory=True)
    subprocess.run(['dotnet', 'restore', str(project / 'Empire.csproj'), '--ignore-failed-sources'], check=True, capture_output=True)
    originals = {file: (project / 'Presentation' / file).read_text() for file, *_ in mutants.values()}
    for name, (file, old, new, expected) in mutants.items():
        for path, content in originals.items():
            (project / 'Presentation' / path).write_text(content)
        target = project / 'Presentation' / file
        original = originals[file]
        assert old in original, name
        target.write_text(original.replace(old, new))
        build = subprocess.run(['dotnet', 'build', str(project / 'Empire.csproj'), '--no-restore', '-v:q'], capture_output=True, text=True)
        if build.returncode:
            raise RuntimeError('Compilation failure is not a killed mutant: ' + name + '\n' + build.stdout + build.stderr)
        result = subprocess.run([os.environ['GODOT_NET'], '--headless', '--path', str(project), 'res://Scenes/BoardDecorationChecks.tscn'], capture_output=True, text=True, timeout=90)
        if result.returncode == 0 or expected not in result.stdout + result.stderr:
            raise RuntimeError('Unrelated failure or survived: ' + name + '\n' + result.stdout + result.stderr)
        print('Killed board decoration mutant: ' + name, flush=True)
