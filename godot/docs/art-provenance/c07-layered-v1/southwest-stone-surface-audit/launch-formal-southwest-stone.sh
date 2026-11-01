#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_CANDIDATE=1 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_APPEARANCE_FACING=east C07_STATIC_FOCUS=14,53
for spec in '2.5 14,56 back' '2.5 15,59 front' '1.25 14,56 context';do
 read -r C07_STATIC_ZOOM C07_STATIC_ACTOR_POSE label <<< "$spec";export C07_STATIC_ZOOM C07_STATIC_ACTOR_POSE
 export C07_STATIC_LABEL="C07 SOUTHWEST STONE | this gate's own five support regions | STATIC $label $C07_STATIC_ZOOM x
Left leaf absent; old low-stone shape difference retained. Other sites/walls incomplete. No commands or saves."
 for kind in reference formal;do
  if [ "$kind" = reference ];then export C07_STATIC_SCENE=res://selected-southwest-before-scene.json C07_STATIC_SW_GATE=1;else export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_STATIC_SW_GATE=0;fi
  export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/formal-southwest-${label}-${kind}.png"
  test ! -e "$C07_STATIC_OUTPUT"
  "$GODOT_NET" --audio-driver Dummy --resolution 1180x760 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/formal-southwest-${label}-${kind}-image.log" 2>&1
 done
done
