#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data"
export C07_SURFACE_PERF_VARIANT=selected C07_SURFACE_PERF_OUTPUT="$ROOT/c07-static-preview/depth-ground-profile.json"
test ! -e "$C07_SURFACE_PERF_OUTPUT"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://FullSceneFrameProbe.tscn > "$ROOT/c07-static-preview/depth-ground-profile.log" 2>&1
