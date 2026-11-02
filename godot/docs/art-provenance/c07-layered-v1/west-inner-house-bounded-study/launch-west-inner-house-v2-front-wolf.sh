#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=33,25 C07_STATIC_ZOOM=4 C07_STATIC_ACTOR_KEY=raider-1 C07_STATIC_ACTOR_POSE=31,30 C07_STATIC_APPEARANCE_FACING=east
export C07_STATIC_SCENE=res://west-inner-house-study.json
export C07_STATIC_LABEL="WEST INNER COTTAGE V2 | UNSELECTED SINGLE-HOUSE STUDY
Actual C07 raider-1 wolf31,30 east FRONT; original doorway witnesses remain visible. Neighbors absent.
No commands, AI, turns or saves. Not a new gameplay result."
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/west-inner-house-v2-front-wolf.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/west-inner-house-v2-front-wolf.log" 2>&1
