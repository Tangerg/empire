#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_CANDIDATE=1 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_APPEARANCE_FACING=east C07_STATIC_FOCUS=25,43 C07_STATIC_ZOOM=1.25
for spec in 'res://selected-council-before-scene.json 28,46 before' 'res://data/scenes/c01-07-selected-assembly.json 28,46 front' 'res://data/scenes/c01-07-selected-assembly.json 25,40 roof';do
 read -r C07_STATIC_SCENE C07_STATIC_ACTOR_POSE label <<< "$spec"
 export C07_STATIC_SCENE C07_STATIC_ACTOR_POSE C07_STATIC_OUTPUT="$ROOT/c07-static-preview/selected-council-${label}.png"
 export C07_STATIC_LABEL="C07 SELECTED ASSEMBLY | council hall $label | diagnostic 1.25x
INCOMPLETE: other buildings/yard/walls missing or legacy placeholders. STATIC / no commands or saves."
 test ! -e "$C07_STATIC_OUTPUT"
 "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/selected-council-${label}-image.log" 2>&1
done
