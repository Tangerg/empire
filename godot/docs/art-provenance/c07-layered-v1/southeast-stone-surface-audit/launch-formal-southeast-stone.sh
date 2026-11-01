#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_CANDIDATE=1 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_APPEARANCE_FACING=east C07_STATIC_FOCUS=82,53
for spec in '2.5 81,57 back' '2.5 81,59 front' '1.25 81,57 context';do
 read -r C07_STATIC_ZOOM C07_STATIC_ACTOR_POSE label <<< "$spec";export C07_STATIC_ZOOM C07_STATIC_ACTOR_POSE
 export C07_STATIC_LABEL="C07 SOUTHEAST STONE | original canvas UV / independent legs | STATIC $label $C07_STATIC_ZOOM x
Leaf absent; other sites/placeholders/walls incomplete. No commands or saves. NOT full-scene acceptance."
 for kind in reference formal;do
  if [ "$kind" = reference ];then export C07_STATIC_SCENE=res://selected-southeast-before-scene.json C07_STATIC_SE_GATE=1;else export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_STATIC_SE_GATE=0;fi
  export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/formal-southeast-${label}-${kind}.png"
  test ! -e "$C07_STATIC_OUTPUT"
  "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/formal-southeast-${label}-${kind}-image.log" 2>&1
 done
done
