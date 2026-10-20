#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
if [[ $# -gt 1 || ( $# -eq 1 && $1 != "--verify-guard" ) ]]; then
  echo "Usage: $0 [--verify-guard]" >&2
  exit 2
fi
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-combat.XXXXXXXX")"
trap 'status=$?; if [[ $status -eq 0 ]]; then rm -rf "$scratch"; else echo "Failure artifacts retained at $scratch" >&2; fi' EXIT
cp "$source_dir/Program.cs" "$source_dir/CombatTests.csproj" "$source_dir/NuGet.Config" "$scratch/"
EMPIRE_COMBAT_ORACLE_OUTPUT="$scratch/oracle.json" ./node_modules/.bin/vite-node "$source_dir/generate-oracle.mjs"
dotnet build "$scratch/CombatTests.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/CombatTests.dll" "$scratch/oracle.json"
if [[ ${1:-} == "--verify-guard" ]]; then
  mkdir "$scratch/mutated"
  cp "$root"/godot/Empire.Core/Combat/*.cs "$scratch/mutated/"
  node - "$scratch/mutated/CombatForecasts.cs" <<'NODE'
const fs = require('node:fs');
const file = process.argv[2];
const source = fs.readFileSync(file, 'utf8');
const before = 'new ReactionBehavior("guard", "防御", false, 0.7, false, false)';
const after = 'new ReactionBehavior("guard", "防御", false, 0.8, false, false)';
if (!source.includes(before)) throw new Error('Guard mutation source no longer matches; update this explicit negative control.');
fs.writeFileSync(file, source.replace(before, after));
NODE
  dotnet build "$scratch/CombatTests.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo \
    "-p:EmpireRoot=$root" "-p:EmpireCombatRoot=$scratch/mutated"
  if dotnet "$scratch/bin/Debug/net8.0/CombatTests.dll" "$scratch/oracle.json" >"$scratch/negative-control.log" 2>&1; then
    echo "Guard failed: changing the guard damage multiplier was not detected." >&2
    exit 1
  fi
  grep -q "Mismatch in guard reaction ready: result" "$scratch/negative-control.log"
  echo "PASS negative control: guard multiplier 0.7 -> 0.8 is rejected by the forecast parity guard"
fi
