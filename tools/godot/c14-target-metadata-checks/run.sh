#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
if [[ $# -gt 1 || ( $# -eq 1 && "$1" != --calculate && "$1" != --negative-control ) ]]; then
  printf 'Usage: %s [--calculate|--negative-control]\n' "$0" >&2
  exit 2
fi
timeout --signal=TERM --kill-after=10s 180 dotnet build tools/godot/c14-target-metadata-checks/C14TargetMetadataChecks.csproj --disable-build-servers --nologo -m:1 -p:UseSharedCompilation=false
timeout --signal=TERM --kill-after=10s 180 dotnet tools/godot/c14-target-metadata-checks/bin/Debug/net8.0/C14TargetMetadataChecks.dll "$root" "$@"
