#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
source "$root/tools/godot/env.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-scenery.XXXXXXXX")"
printf 'Scenery verification output: %s\n' "$scratch"
cd "$root"
python3 "$source_dir/check-assets.py"
EMPIRE_SCENERY_ORACLE="$scratch/oracle.json" "$root/node_modules/.bin/vite-node" "$source_dir/generate-oracle.mjs"
dotnet build godot/Empire.csproj --nologo --no-restore
"$GODOT_NET" --headless --editor --path "$root/godot" --import
"$GODOT_NET" --headless --path "$root/godot" res://Scenes/SceneryChecks.tscn -- --scenery-oracle="$scratch/oracle.json"
