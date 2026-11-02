#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data"
for ITEM in A1 B1 B2 A2; do
 if [[ "$ITEM" == A* ]]; then RUNTIME="$ROOT/empire-cost-baseline-1c6c0435/godot/.godot/mono/temp/bin/Debug"; else RUNTIME="$ROOT/empire-c01/godot/.godot/mono/temp/bin/Debug"; fi
 dotnet build "$ROOT/c07-static-preview/Probe.csproj" --no-restore -p:EmpireRuntimePath="$RUNTIME" > "$ROOT/c07-static-preview/depth-abba-$ITEM-build.log" 2>&1
 sha256sum "$RUNTIME/Empire.dll" "$ROOT/c07-static-preview/.godot/mono/temp/bin/Debug/Empire.dll" > "$ROOT/c07-static-preview/depth-abba-$ITEM-runtime.sha256"
 export C07_SURFACE_PERF_VARIANT=selected
 export C07_SURFACE_PERF_OUTPUT="$ROOT/c07-static-preview/depth-abba-$ITEM.json"
 test ! -e "$C07_SURFACE_PERF_OUTPUT"
 timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://FullSceneFrameProbe.tscn > "$ROOT/c07-static-preview/depth-abba-$ITEM.log" 2>&1
done
dotnet build "$ROOT/c07-static-preview/Probe.csproj" --no-restore > "$ROOT/c07-static-preview/depth-abba-restore-current-build.log" 2>&1
