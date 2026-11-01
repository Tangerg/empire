#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_STATIC_CANDIDATE=1 C07_STATIC_ACTOR_KEY=campaign-torren
for spec in '60,12 62,20 1.25 barracks' '33,14 32,20 1.25 granary' '33,14 48,30 0.75 overview';do
 read -r C07_STATIC_ACTOR_POSE C07_STATIC_FOCUS C07_STATIC_ZOOM label <<< "$spec"
 export C07_STATIC_ACTOR_POSE C07_STATIC_FOCUS C07_STATIC_ZOOM C07_STATIC_OUTPUT="$ROOT/c07-static-preview/selected-assembly-${label}-final.png"
 export C07_STATIC_LABEL="C07 SELECTED ASSEMBLY | 11 independent instances / 2 layered site owners | $label | diagnostic zoom $C07_STATIC_ZOOM
INCOMPLETE: other sites use legacy placeholders; walls/yard missing. Official scene unchanged. STATIC / no commands or saves."
 test ! -e "$C07_STATIC_OUTPUT"
 "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/selected-assembly-${label}-final.log" 2>&1
done
