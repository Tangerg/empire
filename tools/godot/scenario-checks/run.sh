#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-scenario.XXXXXXXX")"
echo "Scenario verification artifacts: $scratch"
cp "$source_dir/Program.cs" "$source_dir/ScenarioTests.csproj" "$source_dir/NuGet.Config" "$scratch/"
EMPIRE_SCENARIO_ORACLE_OUTPUT="$scratch/oracle.json" ./node_modules/.bin/vite-node "$source_dir/generate-oracle.mjs"
dotnet build "$scratch/ScenarioTests.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/ScenarioTests.dll" "$scratch/oracle.json"
if [[ "${1:-}" == "--verify-guard" ]]; then
  mkdir "$scratch/mutant"
  cp "$root/godot/Empire.Core/Terrain/"*.cs "$scratch/mutant/"
  node - "$scratch/mutant/BattleOverlays.cs" <<'JS'
const fs = require('node:fs');
const path = process.argv[2], before = fs.readFileSync(path, 'utf8');
const needle = 'if (remaining <= 0) Remove(state, Text(overlay, "id"), emit);';
if (!before.includes(needle)) throw new Error('Explicit overlay expiry guard target changed.');
fs.writeFileSync(path, before.replace(needle, 'if (remaining < 0) Remove(state, Text(overlay, "id"), emit);'));
JS
  dotnet build "$scratch/ScenarioTests.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo \
    "-p:EmpireRoot=$root" "-p:EmpireTerrainRoot=$scratch/mutant" "-p:OutputPath=$scratch/mutant-bin"
  if dotnet "$scratch/mutant-bin/ScenarioTests.dll" "$scratch/oracle.json" >"$scratch/guard.log" 2>&1; then
    echo 'FAIL: deliberately delayed overlay expiry was not detected' >&2; exit 1
  fi
  if ! grep -q 'Mismatch in overlay round-clock 1: events' "$scratch/guard.log"; then
    cat "$scratch/guard.log" >&2; echo 'FAIL: mutant stopped for an unrelated reason' >&2; exit 1
  fi
  echo 'PASS: event parity guard detects deliberately delayed overlay expiry'
fi
