#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
frozen=false
verify_guards=false
for option in "$@"; do
  case "$option" in
    --frozen) frozen=true ;;
    --verify-guards) verify_guards=true ;;
    *) echo "Usage: $0 [--frozen] [--verify-guards]" >&2; exit 2 ;;
  esac
done
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
source_root="${EMPIRE_SOURCE_ROOT:-$root}"
source "$source_root/tools/godot/env.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-scenario-extensions.XXXXXXXX")"
echo "Scenario extension verification artifacts: $scratch"
if $frozen; then
  oracle="$root/godot/tests/oracle/scenario-extensions.json.gz"
else
  oracle="$scratch/oracle.json"
  EMPIRE_SOURCE_ROOT="$source_root" EMPIRE_SCENARIO_EXTENSION_OUTPUT="$oracle" "$source_root/node_modules/.bin/vite-node" "$source_dir/oracle.ts"
fi
dotnet build "$source_dir/ScenarioExtensionChecks.csproj" --configfile "$source_dir/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$source_dir/bin/Debug/net8.0/ScenarioExtensionChecks.dll" "$oracle"
if $verify_guards; then python3 "$source_dir/mutants.py" "$root" "$oracle"; fi
