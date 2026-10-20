#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
source "$root/tools/godot/env.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-lighting.XXXXXXXX")"
printf 'Lighting verification output: %s\n' "$scratch"
cd "$root"
EMPIRE_LIGHTING_ORACLE="$scratch/oracle.json" "$root/node_modules/.bin/vite-node" "$source_dir/generate-oracle.mjs"
dotnet build godot/Empire.csproj --nologo --no-restore
"$GODOT_NET" --headless --path "$root/godot" res://Scenes/LightingChecks.tscn -- --lighting-oracle="$scratch/oracle.json"
