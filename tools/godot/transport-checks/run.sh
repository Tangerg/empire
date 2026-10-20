#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-transport.XXXXXXXX")"
# Keep artifacts for inspection; this verifier never deletes files.
echo "Transport verification artifacts: $scratch"
cp "$source_dir/Program.cs" "$source_dir/TransportTests.csproj" "$source_dir/NuGet.Config" "$scratch/"
EMPIRE_TRANSPORT_ORACLE_OUTPUT="$scratch/oracle.json" ./node_modules/.bin/vite-node "$source_dir/generate-oracle.mjs"
dotnet build "$scratch/TransportTests.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/TransportTests.dll" "$scratch/oracle.json"
if [[ "${1:-}" == "--verify-guard" ]]; then
  mkdir "$scratch/mutant"
  cp "$root/godot/Empire.Core/Transport/"*.cs "$scratch/mutant/"
  node - "$scratch/mutant/BattleTransport.cs" <<'JS'
const fs = require('node:fs');
const path = process.argv[2];
const before = fs.readFileSync(path, 'utf8');
const needle = 'snapshot["capture"] = 0;';
if (!before.includes(needle)) throw new Error('Guard target no longer exists; update the explicit mutation.');
fs.writeFileSync(path, before.replace(needle, 'snapshot["capture"] = 37;'));
JS
  dotnet build "$scratch/TransportTests.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo \
    "-p:EmpireRoot=$root" "-p:EmpireTransportRoot=$scratch/mutant" "-p:OutputPath=$scratch/mutant-bin"
  if dotnet "$scratch/mutant-bin/TransportTests.dll" "$scratch/oracle.json" >"$scratch/guard.log" 2>&1; then
    echo 'FAIL: deliberate boarding capture-reset defect was not detected' >&2
    exit 1
  fi
  if ! grep -q 'Mismatch in boarding ordinary passenger: hashes' "$scratch/guard.log"; then
    cat "$scratch/guard.log" >&2
    echo 'FAIL: mutant stopped for a reason other than the intended semantic defect' >&2
    exit 1
  fi
  echo 'PASS: emission-time hash guard detects deliberately broken boarding capture reset'
fi
