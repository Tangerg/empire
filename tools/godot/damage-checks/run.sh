#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-damage.XXXXXXXX")"
trap 'rm -rf "$scratch"' EXIT
cp "$source_dir/Program.cs" "$scratch/Program.cs"
cp "$source_dir/DamageTests.csproj" "$source_dir/NuGet.Config" "$scratch/"
EMPIRE_DAMAGE_ORACLE_OUTPUT="$scratch/oracle.json" \
  ./node_modules/.bin/vite-node "$source_dir/generate-oracle.mjs"
dotnet build "$scratch/DamageTests.csproj" --configfile "$scratch/NuGet.Config" \
  --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/DamageTests.dll" "$root" "$scratch/oracle.json"
