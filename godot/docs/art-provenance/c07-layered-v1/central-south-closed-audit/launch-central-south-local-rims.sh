#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for var in ${!C07_STATIC_@};do unset "$var";done
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" C07_STATIC_FOCUS=48,31 C07_STATIC_ZOOM=5 C07_STATIC_ACTOR_KEY=campaign-torren C07_STATIC_ACTOR_POSE=45,34 C07_STATIC_APPEARANCE_FACING=east
export C07_STATIC_SCENE=res://central-south-reconstructed.json
export C07_STATIC_LABEL="SOUTH CENTRAL BASE GROUP | STATIC NEW-DESIGN STUDY, NOT SELECTED
Left complete UV foundation + reused right planter + low pier; original bench/finial absent.
No gameplay/save. Ground / step contact and pairwise geometry UNDER REVIEW."
export C07_STATIC_OUTPUT="$ROOT/c07-static-preview/central-south-reconstructed-local-rims.png"
test ! -e "$C07_STATIC_OUTPUT"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://CityPreview.tscn > "$ROOT/c07-static-preview/central-south-reconstructed-local-rims.log" 2>&1
