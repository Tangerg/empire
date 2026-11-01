#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_CANDIDATE=1 C07_STATIC_FOCUS=25,12
for spec in '2.5 engineer 26,12 east back' '2.5 campaign-laiya 24,15 west front' '1.25 campaign-laiya 24,15 west context';do
 read -r C07_STATIC_ZOOM C07_STATIC_ACTOR_KEY C07_STATIC_ACTOR_POSE C07_STATIC_APPEARANCE_FACING label <<< "$spec"
 export C07_STATIC_ZOOM C07_STATIC_ACTOR_KEY C07_STATIC_ACTOR_POSE C07_STATIC_APPEARANCE_FACING
 export C07_STATIC_LABEL="C07 GRANARY STOCK | independent bales, post enclosure and shed | STATIC $label
Selected local group only; other sites/walls and art integration incomplete. No commands, saves or gameplay."
 for kind in reference formal;do
  if [ "$kind" = reference ];then export C07_STATIC_SCENE=res://selected-column-box-before-scene.json C07_STATIC_GRANARY_COLUMN_BOX=1;else export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_STATIC_GRANARY_COLUMN_BOX=0;fi
  export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/formal-granary-stock-${label}-${kind}.png"
  test ! -e "$C07_STATIC_OUTPUT"
  timeout 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/formal-granary-stock-${label}-${kind}-image.log" 2>&1
 done
done
