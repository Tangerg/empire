#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
source "$root/tools/godot/env.sh"
python3 - "$root" "${1:-}" <<'PY'
import pathlib
import shutil
import subprocess
import sys
import tempfile

root = pathlib.Path(sys.argv[1])
verify_guard = sys.argv[2] == '--verify-guard'
if sys.argv[2] not in ('', '--verify-guard'):
    raise SystemExit('Usage: run.sh [--verify-guard]')

with tempfile.TemporaryDirectory(prefix='empire-relocation-') as scratch:
    scratch = pathlib.Path(scratch)
    core = scratch / 'Empire.Core'
    shutil.copytree(root / 'godot/Empire.Core', core, ignore=shutil.ignore_patterns('bin', 'obj'))
    checks = scratch / 'Checks'
    checks.mkdir()
    source = root / 'tools/godot/relocation-checks'
    for name in ('Program.cs', 'RelocationChecks.csproj'):
        shutil.copy2(source / name, checks / name)
    # No packages are required. Keep restore local and independent of user NuGet configuration.
    (scratch / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>\n')
    project = checks / 'RelocationChecks.csproj'
    assembly = checks / 'bin/Debug/net8.0/RelocationChecks.dll'

    def run(command, seconds):
        return subprocess.run(command, cwd=scratch, text=True, stdout=subprocess.PIPE,
                              stderr=subprocess.STDOUT, timeout=seconds)

    def build():
        result = run(['dotnet', 'build', str(project), '--nologo', '--disable-build-servers',
                      '--configfile', str(scratch / 'NuGet.Config'), f'-p:EmpireCoreProject={core / "Empire.Core.csproj"}'], 120)
        if result.returncode:
            raise RuntimeError('Build failed; an uncompilable mutant is not a killed mutant:\n' + result.stdout)

    def execute():
        return run(['dotnet', str(assembly), str(root)], 30)

    build()
    baseline = execute()
    print(baseline.stdout, end='')
    if baseline.returncode:
        raise SystemExit('FAIL: baseline relocation checks failed')
    if verify_guard:
        mutants = [
            ('origin capture retained', 'Tactics/BattleRelocation.cs',
             'capture.Progress[capture.Index] = 0;\n        Place(unit, destination);',
             'Place(unit, destination);', 'move clears only origin capture'),
            ('invalid destination admitted', 'Tactics/BattleRelocation.cs',
             'ValidateVacant(destination, unitId);', '', 'invalid relocation destination accepted'),
            ('boarding capture retained', 'Tactics/BattleRelocation.cs',
             'ClearUnitCapture(snapshot);', '', 'boarding snapshot capture reset'),
            ('teleport consumes action', 'Scenario/ScenarioCommands.Units.cs',
             'new BattleRelocation(state).MoveUnit(Integer(unit, "id"), at);',
             'new BattleRelocation(state).MoveUnit(Integer(unit, "id"), at); unit["done"] = true;',
             'teleport preserves facing readiness and patrol'),
        ]
        for name, relative, needle, replacement, expected in mutants:
            target = core / relative
            original = target.read_text()
            if original.count(needle) != 1:
                raise RuntimeError(f'Mutation target changed: {relative}: {needle}')
            try:
                target.write_text(original.replace(needle, replacement))
                build()
                result = execute()
                if result.returncode == 0 or expected not in result.stdout:
                    raise RuntimeError(f'Mutant not rejected by its intended assertion: {name}\n{result.stdout}')
                print(f'PASS compiled mutant rejected: {name}')
            finally:
                target.write_text(original)
PY
