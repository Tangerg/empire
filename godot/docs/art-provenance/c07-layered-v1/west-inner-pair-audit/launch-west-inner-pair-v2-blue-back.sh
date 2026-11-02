#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=30,26 C07_STATIC_ZOOM=4 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_ACTOR_POSE=28,24 C07_STATIC_APPEARANCE_FACING=east
export C07_STATIC_SCENE=res://west-inner-pair-study.json
export C07_STATIC_LABEL="WEST INNER PAIR V2 | UNSELECTED STATIC STUDY
Actual Torren28,24 east BACK. Blue new-design registration; garden absent; new ground registration north3px; hidden base checked separately.
No commands, AI, turns or saves. Not a new gameplay result."
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/west-inner-pair-v2-blue-back.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/west-inner-pair-v2-blue-back.log" 2>&1
