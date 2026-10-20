#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
source "$root/tools/godot/env.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-utf16.XXXXXXXX")"
printf 'UTF-16 verification output: %s\n' "$scratch"
cd "$root"
EMPIRE_UTF16_ORACLE_OUTPUT="$scratch/oracle.json" \
  "$root/node_modules/.bin/vite-node" "$source_dir/generate-oracle.mjs"
dotnet run --project "$source_dir/Utf16BoundaryChecks.csproj" -- "$scratch/oracle.json" "$root"
