#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_CANDIDATE=1 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_APPEARANCE_FACING=east C07_STATIC_FOCUS=14,53
for spec in '2.5 12,55 back' '2.5 12,57 front' '1.25 12,55 context';do
 read -r C07_STATIC_ZOOM C07_STATIC_ACTOR_POSE label <<< "$spec";export C07_STATIC_ZOOM C07_STATIC_ACTOR_POSE
 export C07_STATIC_LABEL="C07 SOUTHWEST STONE + LEFT LEAF | independent upward-right leaf support | STATIC $label $C07_STATIC_ZOOM x
Old low-stone/art debt retained; separate stone/leaf owners. Other sites/walls incomplete. No commands or saves."
 for kind in reference formal;do
  if [ "$kind" = reference ];then export C07_STATIC_SCENE=res://selected-southwest-leaf-before-scene.json C07_STATIC_SW_LEAF=1;else export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_STATIC_SW_LEAF=0;fi
  export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/formal-southwest-leaf-${label}-${kind}.png"
  test ! -e "$C07_STATIC_OUTPUT"
  "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/formal-southwest-leaf-${label}-${kind}-image.log" 2>&1
 done
done
