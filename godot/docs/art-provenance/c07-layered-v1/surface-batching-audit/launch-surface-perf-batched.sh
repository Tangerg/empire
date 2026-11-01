#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_SURFACE_PERF_VARIANT=formal
i=0
for mode in unbatched batched batched unbatched;do
 i=$((i+1));if [ "$mode" = unbatched ];then project="$ROOT/c07-surface-unbatched-runtime";else project="$ROOT/c07-static-preview";fi
 export C07_SURFACE_PERF_OUTPUT="$ROOT/c07-static-preview/surface-perf-comparison-$i-$mode.json"
 test ! -e "$C07_SURFACE_PERF_OUTPUT"
 "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$project" res://SurfaceFrameProbe.tscn > "$ROOT/c07-static-preview/surface-perf-comparison-$i-$mode.log" 2>&1
done
