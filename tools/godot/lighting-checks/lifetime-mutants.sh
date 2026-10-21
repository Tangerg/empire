#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
source "$root/tools/godot/env.sh"
python3 - "$root" "${1:-all}" <<'PY'
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tempfile

root = Path(sys.argv[1])
source = root / 'godot'
mode = sys.argv[2]
if mode not in ['all', 'construction']:
    raise SystemExit('Usage: lifetime-mutants.sh [all|construction]')
limit = int(os.environ.get('EMPIRE_CHECK_TIMEOUT_SECONDS', '300'))
if limit <= 0:
    raise SystemExit('Process timeout must be positive.')
mutants = [
    ('fallible-construction', 'Presentation/OwnedProceduralSprite.cs',
     'sprite.Free();', '// Deliberate missing construction cleanup.',
     'Failed configuration must free its unattached'),
    ('exclusive-texture', 'Presentation/OwnedProceduralSprite.cs',
     '_ownedTexture.Dispose();', '// Deliberate missing exclusive disposal.',
     'Standalone contact must explicitly dispose'),
    ('generated-material', 'Presentation/OwnedShaderMaterial.cs',
     '_material.Dispose();', '// Deliberate missing generated material disposal.',
     'Removing a ground clip must release'),
    ('svg-texture', 'Presentation/DynamicStateView.cs',
     'sprite = OwnedProceduralSprite.Create(image);',
     'sprite = new Sprite2D { Texture = ImageTexture.CreateFromImage(image) };',
     'Replaced health gauges must release'),
]

def run(command, cwd, env=None):
    return subprocess.run(['timeout', '--signal=TERM', '--kill-after=15s', str(limit), *command],
        cwd=cwd, env=env, capture_output=True, text=True, timeout=limit + 20)

with tempfile.TemporaryDirectory(prefix='empire-lifetime-mutants-') as temporary:
    project = Path(temporary) / 'godot'
    shutil.copytree(source, project, ignore=shutil.ignore_patterns(
        '.godot', 'bin', 'obj', 'assets', 'data', 'tests', '.git'))
    for name in ['assets', 'data', 'tests']:
        (project / name).symlink_to(source / name, target_is_directory=True)
    (project / '.godot').mkdir()
    (project / '.godot/imported').symlink_to(source / '.godot/imported', target_is_directory=True)
    for name, relative, old, new, marker in [('baseline', '', '', '', ''), *(mutants[:1] if mode == 'construction' else mutants)]:
        target = project / relative if relative else None
        original = target.read_text() if target else None
        if target:
            if original.count(old) != 1:
                raise SystemExit(f'{name}: mutation anchor is not unique.')
            target.write_text(original.replace(old, new))
        build = run([str(Path(os.environ['DOTNET_ROOT']) / 'dotnet'), 'build', 'Empire.csproj',
                     '--nologo', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'], project)
        if build.returncode != 0:
            raise SystemExit(f'{name}: invalid experiment, compilation failed.\n{build.stdout}\n{build.stderr}')
        env = dict(os.environ)
        env['XDG_DATA_HOME'] = str(Path(temporary) / ('userdata-' + name))
        result = run([os.environ['GODOT_NET'], '--headless', '--path', str(project),
                      'res://Scenes/LightingLifetimeChecks.tscn', *(['--', '--construction-only'] if mode == 'construction' else [])], project, env)
        output = result.stdout + result.stderr
        if name == 'baseline':
            if result.returncode != 0 or 'Lighting lifetime checks passed:' not in output or any(
                text in output.lower() for text in ['error:', 'leaked', 'segmentation fault']):
                raise SystemExit(f'Baseline failed ({result.returncode}):\n{output}')
        elif result.returncode in [0, 124, 137] or marker not in output:
            raise SystemExit(f'{name}: survived or failed for the wrong reason ({result.returncode}):\n{output}')
        print(f'PASS {name}', flush=True)
        if target:
            target.write_text(original)
PY
