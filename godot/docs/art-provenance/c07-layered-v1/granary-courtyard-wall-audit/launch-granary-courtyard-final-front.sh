#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=21,15 C07_STATIC_ZOOM=4
for variant in selected;do
 if [ "$variant" = original ];then export C07_STATIC_SCENE=res://data/scenes/c01-07.json;else export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json;fi
 export C07_STATIC_LABEL="GRANARY COURTYARD WALL RESPONSIBILITY | STATIC ONLY, NO commands/save
Selected actual Torren (19,15) | old misplaced cut-head mask absent; no gameplay."
 export C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_APPEARANCE_FACING=east C07_STATIC_ACTOR_POSE=19,15
 export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/granary-courtyard-final-torren-front.png"
 test ! -e "$C07_STATIC_OUTPUT"
 timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/granary-courtyard-final-torren-front.log" 2>&1
done
