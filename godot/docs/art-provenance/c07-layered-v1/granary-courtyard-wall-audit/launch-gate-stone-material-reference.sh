#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
timeout -k 10 60 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://GateStoneMaterialReference.tscn > "$ROOT/c07-static-preview/gate-stone-material-reference.log" 2>&1
