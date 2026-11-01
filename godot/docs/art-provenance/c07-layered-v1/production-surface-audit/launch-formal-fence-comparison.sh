#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=60,13 C07_STATIC_ZOOM=4 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_CANDIDATE=1
for mode in before after;do
 if [ "$mode" = before ];then export C07_STATIC_SCENE=res://formal-fence-before-scene.json C07_STATIC_ATOMIC_FENCE=1;else export C07_STATIC_SCENE=res://data/scenes/c01-07-fence-surface-preview.json C07_STATIC_ATOMIC_FENCE=0;fi
 for pose in 60,12 62,14;do
  export C07_STATIC_ACTOR_POSE=$pose C07_STATIC_OUTPUT="$ROOT/c07-static-preview/formal-fence-${mode}-${pose/,/-}.png"
  export C07_STATIC_LABEL="C07 STATIC FENCE | $mode renderer | actor $pose | zoom 4 | NO commands/save; NOT gameplay
ONE OBJECT STUDY; other scene objects absent/placeholders. Official C07 unchanged."
  test ! -e "$C07_STATIC_OUTPUT"
  "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/formal-fence-${mode}-${pose/,/-}.log" 2>&1
 done
done
