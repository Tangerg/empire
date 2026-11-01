#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=31,9 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_CANDIDATE=1 C07_STATIC_APPEARANCE_FACING=east
for mode in previous formal;do
 if [ "$mode" = formal ];then export C07_STATIC_SCENE=res://data/scenes/c01-07-granary-canopy-surface-preview.json C07_STATIC_GRANARY_CANOPY=0;else export C07_STATIC_SCENE=res://data/scenes/c01-07-granary-preview.json C07_STATIC_GRANARY_CANOPY=1;fi
 for spec in '27,13 3 back' '27,15 3 front' '27,13 1.25 scale';do
  read -r C07_STATIC_ACTOR_POSE C07_STATIC_ZOOM label <<< "$spec"
  export C07_STATIC_ACTOR_POSE C07_STATIC_ZOOM C07_STATIC_OUTPUT="$ROOT/c07-static-preview/formal-granary-canopy-${mode}-${label}.png"
  export C07_STATIC_LABEL="C07 GRANARY CANOPY STATIC STUDY | $mode | actor $C07_STATIC_ACTOR_POSE | zoom $C07_STATIC_ZOOM
Main house + canopy; cargo/yard/other objects incomplete. NO commands/save; NOT gameplay."
  test ! -e "$C07_STATIC_OUTPUT"
  "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/formal-granary-canopy-${mode}-${label}.log" 2>&1
 done
done
