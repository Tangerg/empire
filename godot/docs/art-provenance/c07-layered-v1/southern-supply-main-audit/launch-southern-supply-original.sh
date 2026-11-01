#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=77,44 C07_STATIC_ZOOM=2.5
export C07_STATIC_SCENE=res://data/scenes/c01-07.json
export C07_STATIC_LABEL='SOUTHERN SUPPLY ORIGINAL | static source study, no commands/save
Main house, canvas and cargo must retain separate feet; original geometric contracts under review.'
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/southern-supply-original-context.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/southern-supply-original-context.log" 2>&1
