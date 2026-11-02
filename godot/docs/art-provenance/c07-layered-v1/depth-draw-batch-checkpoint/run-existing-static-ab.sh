#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data"
for ITEM in A B; do
 if [[ "$ITEM" == A ]]; then RUNTIME="$ROOT/empire-cost-baseline-1c6c0435/godot/.godot/mono/temp/bin/Debug"; else RUNTIME="$ROOT/empire-c01/godot/.godot/mono/temp/bin/Debug"; fi
 dotnet build "$ROOT/c07-static-preview/Probe.csproj" --no-restore -p:EmpireRuntimePath="$RUNTIME" > "$ROOT/c07-static-preview/static-existing-$ITEM-build.log" 2>&1
 export EXISTING_STATIC_OUTPUT="$ROOT/c07-static-preview/static-existing-$ITEM.json"
 timeout -k 10 300 "$GODOT_NET" --headless --path "$ROOT/c07-static-preview" res://ExistingLevelStaticProbe.tscn > "$ROOT/c07-static-preview/static-existing-$ITEM.log" 2>&1
done
