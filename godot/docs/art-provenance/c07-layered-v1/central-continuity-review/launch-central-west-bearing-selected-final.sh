#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=44,30 C07_STATIC_ZOOM=5 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_ACTOR_POSE=43,32 C07_STATIC_APPEARANCE_FACING=east
for mode in candidate;do
 if [ "$mode" = original ];then export C07_STATIC_SCENE=res://data/scenes/c01-07.json;else export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json;fi
 export C07_STATIC_LABEL="SELECTED WEST GROUND-FLUSH CONNECTION | STATIC INITIAL-STATE DISPLAY CLONE ONLY
actual Torren p43,32 east; NEW GROUND-FLUSH stone connection.
White box / low corner pier independent; no raised partial support. No commands/save."
 export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/central-west-bearing-selected-final-front.png"
 test ! -e "$C07_STATIC_OUTPUT"
 timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/central-west-bearing-selected-final-front.log" 2>&1
done
