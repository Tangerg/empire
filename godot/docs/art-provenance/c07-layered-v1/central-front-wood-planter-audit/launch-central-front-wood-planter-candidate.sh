#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=52,30 C07_STATIC_ZOOM=5 C07_STATIC_SCENE=res://central-front-wood-planter-candidate.json C07_STATIC_ACTOR_KEY=campaign-torren
for pose in back front;do
 if [ "$pose" = back ];then export C07_STATIC_ACTOR_POSE=53,29 C07_STATIC_APPEARANCE_FACING=west;else export C07_STATIC_ACTOR_POSE=51,32 C07_STATIC_APPEARANCE_FACING=east;fi
 export C07_STATIC_LABEL="FRONT WOOD PLANTER STATIC CANDIDATE | STATIC INITIAL-STATE DISPLAY CLONE ONLY
$pose actual Torren; front wood box with OWN stone plinth; rear white box separate; bench absent. NEW ground placement, no commands/save."
 export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/central-front-wood-planter-candidate-$pose.png"
 test ! -e "$C07_STATIC_OUTPUT"
 timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/central-front-wood-planter-candidate-$pose.log" 2>&1
done
