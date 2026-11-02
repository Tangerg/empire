#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=30,27 C07_STATIC_ZOOM=2.5 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_ACTOR_POSE=28,30 C07_STATIC_APPEARANCE_FACING=east
for MODE in rear-candidate; do
 if [[ "$MODE" == original ]]; then export C07_STATIC_SCENE=res://data/scenes/c01-07.json; else export C07_STATIC_SCENE=res://west-inner-rear-house-study.json; fi
 export C07_STATIC_LABEL="WEST INNER BLOCK | $MODE STATIC REVIEW
NEW rear annex + existing pair/right court. Left enclosure/yard absent. Registration and full footprint NOT validated.
Actual appearance display clone only. No commands, AI, turns or saves. Not gameplay or final acceptance."
 export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/west-inner-block-$MODE-review.png"
 test ! -e "$C07_STATIC_OUTPUT"
 timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/west-inner-block-$MODE-review.log" 2>&1
done
