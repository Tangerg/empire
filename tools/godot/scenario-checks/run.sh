#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-scenario.XXXXXXXX")"
trap 'status=$?; if [[ $status -eq 0 ]]; then rm -rf "$scratch"; else echo "Failure artifacts retained at $scratch" >&2; fi' EXIT
cp "$source_dir/Program.cs" "$source_dir/ScenarioTests.csproj" "$source_dir/NuGet.Config" "$scratch/"
EMPIRE_SCENARIO_ORACLE_OUTPUT="$scratch/oracle.json" ./node_modules/.bin/vite-node "$source_dir/generate-oracle.mjs"
dotnet build "$scratch/ScenarioTests.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/ScenarioTests.dll" "$scratch/oracle.json"
