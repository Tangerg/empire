#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
fixture_root="${EMPIRE_FIXTURE_ROOT:-$root}"
source "${EMPIRE_ENV_FILE:-$root/tools/godot/env.sh}"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-spatial-extensions.XXXXXXXX")"
echo "Spatial extension artifacts: $scratch"
cp "$source_dir/Program.cs" "$source_dir/SpatialExtensionChecks.csproj" "$source_dir/NuGet.Config" "$scratch/"
dotnet build "$scratch/SpatialExtensionChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/SpatialExtensionChecks.dll" "$fixture_root/godot/tests/oracle/tactical-queries.json.gz" "$fixture_root/godot/tests/oracle/movement-vectors.json" "$root/godot/tests/oracle/spatial-visibility.json.gz"
python3 "$source_dir/architecture.py" "$root"
if [[ "${1:-}" == "--verify-guard" ]]; then
    python3 "$source_dir/mutants.py" "$root" "$fixture_root" "$scratch"
fi
