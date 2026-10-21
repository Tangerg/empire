#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/godot/env.sh
if [[ $# != 1 || "$1" != /* || -e "$1" ]]; then
  echo 'Usage: bash tools/godot/export-macos.sh /absolute/new-output-directory' >&2
  echo 'The output directory must not exist. Previous builds and saves are preserved.' >&2
  exit 2
fi
if [[ "$(uname -sm)" != 'Linux x86_64' ]]; then
  echo 'This cross-export workflow has only been checked on Linux x86_64.' >&2
  exit 2
fi
if [[ ! -x "$DOTNET_ROOT/dotnet" || ! -x "$GODOT_NET" ]]; then
  echo 'The workspace-local official .NET 8 SDK and Godot 4.6.3 .NET editor are required.' >&2
  exit 1
fi
version="$(timeout --kill-after=10s 60s "$GODOT_NET" --headless --version)"
if [[ "$version" != '4.6.3.stable.mono.official.7d41c59c4' ]]; then
  printf 'Unexpected Godot toolchain: %s\n' "$version" >&2
  exit 1
fi
templates="$XDG_DATA_HOME/godot/export_templates/4.6.3.stable.mono"
for dependency in macos.zip icudt_godot.dat version.txt; do
  test -s "$templates/$dependency" || { printf 'Missing matching template: %s\n' "$dependency" >&2; exit 1; }
done
for notice in README.txt manifest.json GODOT-LICENSE.txt GODOT-COPYRIGHT.txt DOTNET-LICENSE.txt DOTNET-THIRD-PARTY-NOTICES.txt; do
  test -s "distribution/notices/$notice" || { printf 'Missing distribution notice: %s\n' "$notice" >&2; exit 1; }
done
output="$1"
mkdir -p "$output/logs" "$output/tmp" "$output/cache" "$output/package"
# Own the export temporary directory. Do not touch another build or any user save.
export TMPDIR="$output/tmp" XDG_CACHE_HOME="$output/cache"
timeout --signal=TERM --kill-after=30s 20m dotnet build godot/Empire.csproj --nologo 2>&1 | tee "$output/logs/build.log"
timeout --signal=TERM --kill-after=30s 15m "$GODOT_NET" --headless --editor --path godot --import 2>&1 | tee "$output/logs/import.log"
archive="$output/Empire-macos-universal-UNTESTED.zip"
timeout --signal=TERM --kill-after=30s 30m "$GODOT_NET" --headless --path godot --export-release 'macOS Universal (UNTESTED)' "$output/package/Empire.app" 2>&1 | tee "$output/logs/export.log"
for log in build import export; do
  if grep -Eiq '(^ERROR:|SCRIPT ERROR:|segmentation fault|unhandled exception)' "$output/logs/$log.log"; then
    printf 'Build/export diagnostics failed: %s\n' "$output/logs/$log.log" >&2
    exit 1
  fi
done
cp -R distribution/notices "$output/package/THIRD-PARTY-NOTICES"
# Archive only after Godot has exited. Our completed direct-ZIP workflow failed
# executable bounds checks; no binary or signature is altered here.
python3 - "$archive" "$output/package" <<'PY'
from pathlib import Path
import json, os, subprocess, sys, zipfile
source_commit = os.environ.get('EMPIRE_SOURCE_COMMIT')
if not source_commit:
    result = subprocess.run(['git', 'rev-parse', 'HEAD'], capture_output=True, text=True)
    source_commit = result.stdout.strip() if result.returncode == 0 else 'unversioned source'
package = Path(sys.argv[2])
with zipfile.ZipFile(sys.argv[1], 'x', compression=zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(package.rglob('*')):
        if path.is_symlink():
            raise RuntimeError('Unexpected bundle symlink: ' + str(path))
        if path.is_file():
            archive.write(path, path.relative_to(package))
    archive.write('godot/docs/MACOS-PACKAGE.md', 'README-UNTESTED.md')
    archive.writestr('SOURCE-PROVENANCE.json', json.dumps({
        'source_commit': source_commit,
        'export_configuration': 'macOS Universal (UNTESTED)',
        'godot': '4.6.3.stable.mono.official.7d41c59c4',
        'host': 'Linux x86_64', 'macos_execution': 'NOT TESTED'
    }, indent=2) + '\n')
PY
python3 tools/godot/verify-macos-package.py "$archive" | tee "$output/STATIC-VERIFICATION.json"
(cd "$output" && sha256sum Empire-macos-universal-UNTESTED.zip) | tee "$output/SHA256SUMS"
printf 'macOS cross-export and static inspection passed; NOT executed on macOS: %s\n' "$output"
