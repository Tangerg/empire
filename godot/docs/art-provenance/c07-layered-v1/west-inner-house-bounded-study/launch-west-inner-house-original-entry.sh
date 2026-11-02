#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=33,27 C07_STATIC_ZOOM=4 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_ACTOR_POSE=34,30 C07_STATIC_APPEARANCE_FACING=west
export C07_STATIC_SCENE=res://data/scenes/c01-07.json
export C07_STATIC_LABEL="WEST INNER COTTAGE ORIGINAL | STATIC REFERENCE ONLY
Actual Torren34,30 west display clone at legal approach; red house / blue neighbor / garden separate.
No commands, AI, turns or saves. Not a new gameplay result."
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/west-inner-house-original-entry.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/west-inner-house-original-entry.log" 2>&1
