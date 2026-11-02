#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export C07_SOURCE_REFERENCE=res://central-landmark-source-reference.json
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data"
test ! -e "$ROOT/c07-static-preview/central-landmark-original-source.png"
timeout -k 10 90 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://C07SourceReference.tscn > "$ROOT/c07-static-preview/central-landmark-original-source.log" 2>&1
