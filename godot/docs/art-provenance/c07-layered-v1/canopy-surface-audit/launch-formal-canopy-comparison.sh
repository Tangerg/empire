#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=60,12 C07_STATIC_ZOOM=4 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_CANDIDATE=1
for mode in original inset formal;do
 if [ "$mode" = formal ];then export C07_STATIC_SCENE=res://data/scenes/c01-07-canopy-surface-preview.json C07_STATIC_ATOMIC_FENCE=0 C07_STATIC_ATOMIC_CANOPY=0;else
 export C07_STATIC_SCENE=res://data/scenes/c01-07-front-stock-preview.json C07_STATIC_ATOMIC_FENCE=1 C07_STATIC_ATOMIC_CANOPY=1
 if [ "$mode" = inset ];then export C07_STATIC_CANOPY_SOURCE=res://atomic-canopy-surface-source.json;else export C07_STATIC_CANOPY_SOURCE=res://atomic-canopy-geometry.json;fi
 fi
 for pose in 60,12 60,11;do
  export C07_STATIC_ACTOR_POSE=$pose C07_STATIC_OUTPUT="$ROOT/c07-static-preview/formal-canopy-${mode}-${pose/,/-}.png"
  export C07_STATIC_LABEL="C07 BARRACKS FRONT STATIC STUDY | $mode | actor $pose | zoom 4 | NO commands/save
Core + canopy + cargo + fence + blue prop. Other scene objects incomplete; NOT gameplay."
  test ! -e "$C07_STATIC_OUTPUT"
  "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/formal-canopy-${mode}-${pose/,/-}.log" 2>&1
 done
done
