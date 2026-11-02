#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=25,14 C07_STATIC_ZOOM=4
for variant in selected;do
 if [ "$variant" = original ];then export C07_STATIC_SCENE=res://data/scenes/c01-07.json;else export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json;fi
 export C07_STATIC_LABEL="GRANARY COURTYARD WALL RESPONSIBILITY | STATIC ONLY, NO commands/save
Independent main wall + terminal pier | left fence/low stone extension absent; geometry candidate."
 export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/granary-courtyard-wall-candidate-context.png"
 test ! -e "$C07_STATIC_OUTPUT"
 timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/granary-courtyard-wall-candidate-context.log" 2>&1
done
