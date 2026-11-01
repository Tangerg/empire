#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data"
i=0
for mode in previous formal formal previous;do
 i=$((i+1));export C07_SURFACE_PERF_VARIANT="$mode" C07_SURFACE_PERF_OUTPUT="$ROOT/c07-static-preview/surface-perf-baseline-$i-$mode.json"
 test ! -e "$C07_SURFACE_PERF_OUTPUT"
 "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://SurfaceFrameProbe.tscn > "$ROOT/c07-static-preview/surface-perf-baseline-$i-$mode.log" 2>&1
done
