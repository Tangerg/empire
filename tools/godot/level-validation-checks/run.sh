#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-level-validation.XXXXXXXX")"
echo "Level validation artifacts: $scratch"
cp "$source_dir/Program.cs" "$source_dir/ValidationChecks.csproj" "$source_dir/NuGet.Config" "$scratch/"
./node_modules/.bin/tsc --noEmit --allowJs --checkJs --target es2022 --module esnext --moduleResolution bundler --skipLibCheck --types node "$source_dir/generate-oracle.mjs"
EMPIRE_LEVEL_VALIDATION_ORACLE_OUTPUT="$scratch/oracle.json" ./node_modules/.bin/vite-node "$source_dir/generate-oracle.mjs"
dotnet build "$scratch/ValidationChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/ValidationChecks.dll" "$scratch/oracle.json"
