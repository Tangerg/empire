#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data"
for mode in unbatched batched;do
 if [ "$mode" = unbatched ];then project="$ROOT/c07-surface-unbatched-runtime";else project="$ROOT/c07-static-preview";fi
 for zoom in 1 1.25 2;do
  for shift in 0 0.5;do
   export C07_BLEND_ZOOM=$zoom C07_BLEND_SHIFT=$shift C07_BLEND_REVERSE=0 C07_BLEND_OUTPUT="$ROOT/c07-static-preview/surface-blend-$mode-z$zoom-s$shift.png"
   test ! -e "$C07_BLEND_OUTPUT"
   "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$project" res://SurfaceBlendProbe.tscn > "$ROOT/c07-static-preview/surface-blend-$mode-z$zoom-s$shift.log" 2>&1
  done
 done
done
export C07_BLEND_ZOOM=2 C07_BLEND_SHIFT=0.5 C07_BLEND_REVERSE=1 C07_BLEND_OUTPUT="$ROOT/c07-static-preview/surface-blend-reversed-z2-s0.5.png"
test ! -e "$C07_BLEND_OUTPUT"
"$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://SurfaceBlendProbe.tscn > "$ROOT/c07-static-preview/surface-blend-reversed-z2-s0.5.log" 2>&1
for var in ${!C07_STATIC_@};do unset "$var";done
export C07_STATIC_FOCUS=60,13 C07_STATIC_ZOOM=4 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_CANDIDATE=1 C07_STATIC_SCENE=res://data/scenes/c01-07-fence-surface-preview.json C07_STATIC_ATOMIC_FENCE=0
for pose in 60,12 62,14;do
 export C07_STATIC_ACTOR_POSE=$pose C07_STATIC_OUTPUT="$ROOT/c07-static-preview/surface-batch-real-${pose/,/-}.png"
 export C07_STATIC_LABEL="C07 STATIC FENCE | after renderer | actor $pose | zoom 4 | NO commands/save; NOT gameplay
ONE OBJECT STUDY; other scene objects absent/placeholders. Official C07 unchanged."
 test ! -e "$C07_STATIC_OUTPUT"
 "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/surface-batch-real-${pose/,/-}.log" 2>&1
done
