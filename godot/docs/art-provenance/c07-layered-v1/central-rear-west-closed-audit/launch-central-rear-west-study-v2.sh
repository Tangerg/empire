#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=45,27 C07_STATIC_ZOOM=5 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_ACTOR_POSE=46,26 C07_STATIC_APPEARANCE_FACING=west
export C07_STATIC_SCENE=res://central-rear-west-study.json
export C07_STATIC_LABEL="WEST REAR SEAT + BED | UNSELECTED GEOMETRY STUDY
Thin seat + four actual leg bases; independent inner bed is NEW design. Not final art.
No commands, AI, turns or saves. Not a new gameplay result."
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/central-rear-west-study-v2.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/central-rear-west-study-v2.log" 2>&1
