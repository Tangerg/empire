#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/godot/env.sh
if [[ $# != 1 || "$1" != /* || -e "$1" ]]; then
  echo 'Usage: bash tools/godot/export-linux.sh /absolute/new-output-directory' >&2
  echo 'The output directory must not already exist. No previous build or save is removed.' >&2
  exit 2
fi
output="$1"
if [[ "$(uname -sm)" != 'Linux x86_64' ]]; then
  echo 'This export-and-smoke workflow is verified only on Linux x86_64.' >&2
  exit 2
fi
if [[ ! -x "$DOTNET_ROOT/dotnet" || ! -x "$GODOT_NET" ]]; then
  echo 'Install the official .NET 8 SDK and Godot 4.6.3 .NET tools in EMPIRE_TOOLCHAIN_ROOT first.' >&2
  exit 1
fi
version="$(timeout --kill-after=10s 60s "$GODOT_NET" --headless --version)"
if [[ "$version" != '4.6.3.stable.mono.official.7d41c59c4' ]]; then
  printf 'Unexpected Godot toolchain: %s\n' "$version" >&2
  exit 1
fi
templates="$XDG_DATA_HOME/godot/export_templates/4.6.3.stable.mono"
for file in linux_release.x86_64 icudt_godot.dat version.txt; do
  test -f "$templates/$file" || { printf 'Missing matching export dependency: %s\n' "$templates/$file" >&2; exit 1; }
done
for notice in README.txt manifest.json GODOT-LICENSE.txt GODOT-COPYRIGHT.txt DOTNET-LICENSE.txt DOTNET-THIRD-PARTY-NOTICES.txt; do
  test -s "distribution/notices/$notice" || { printf 'Missing distribution notice: %s\n' "$notice" >&2; exit 1; }
done
mkdir -p "$output/package" "$output/logs"
timeout --signal=TERM --kill-after=30s 20m dotnet build godot/Empire.csproj --nologo 2>&1 | tee "$output/logs/build.log"
timeout --signal=TERM --kill-after=30s 15m "$GODOT_NET" --headless --editor --path godot --import 2>&1 | tee "$output/logs/import.log"
timeout --signal=TERM --kill-after=30s 30m "$GODOT_NET" --headless --path godot --export-release 'Linux x86_64' "$output/package/Empire.x86_64" 2>&1 | tee "$output/logs/export.log"
for log in build import export; do
  if grep -Eiq '(^ERROR:|SCRIPT ERROR:|segmentation fault|unhandled exception)' "$output/logs/$log.log"; then
    printf 'Build/export diagnostics failed: %s\n' "$output/logs/$log.log" >&2
    exit 1
  fi
done
test -x "$output/package/Empire.x86_64"
test -s "$output/package/Empire.pck"
test -s "$output/package/data_Empire_linuxbsd_x86_64/Empire.dll"
test -s "$output/package/data_Empire_linuxbsd_x86_64/libcoreclr.so"
bash tools/godot/smoke-linux-package.sh "$output/package/Empire.x86_64" 2>&1 | tee "$output/logs/package-smoke.log"
cp godot/docs/LINUX-PACKAGE.md "$output/package/README.md"
cp -R distribution/notices "$output/package/THIRD-PARTY-NOTICES"
tar -czf "$output/Empire-linux-x86_64.tar.gz" -C "$output/package" .
sha256sum "$output/Empire-linux-x86_64.tar.gz" | tee "$output/SHA256SUMS"
printf 'Linux x86_64 export and packaged smoke passed: %s\n' "$output"
