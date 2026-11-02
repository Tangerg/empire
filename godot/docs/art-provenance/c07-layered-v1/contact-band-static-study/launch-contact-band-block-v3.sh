#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" CONTACT_BAND_OUTPUT="$ROOT/c07-static-preview/contact-band-block-v3"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://ContactShadowBandStudy.tscn > "$ROOT/c07-static-preview/contact-band-block-v3-native.log" 2>&1
