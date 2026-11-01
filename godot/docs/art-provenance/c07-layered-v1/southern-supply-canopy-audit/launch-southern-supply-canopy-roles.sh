#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=77,44 C07_STATIC_ZOOM=2.5
export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json
export C07_STATIC_LABEL='SOUTHERN SUPPLY MAIN CANDIDATE | STATIC ONLY, NO commands/save
Actual Torren in FRONT of west post; main/cloth/posts only, cargo absent, no gameplay.'
export C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_APPEARANCE_FACING=east
export C07_STATIC_ACTOR_POSE=67,47
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/southern-supply-canopy-west-front.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/southern-supply-canopy-west-front.log" 2>&1
export C07_STATIC_LABEL='SOUTHERN SUPPLY MAIN CANDIDATE | STATIC ONLY, NO commands/save
Actual engineer BEHIND cloth; main/cloth/posts only, cargo absent, no gameplay.'
export C07_STATIC_ACTOR_KEY=engineer C07_STATIC_APPEARANCE_FACING=east
export C07_STATIC_ACTOR_POSE=69,44
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/southern-supply-canopy-cloth-back.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/southern-supply-canopy-cloth-back.log" 2>&1
