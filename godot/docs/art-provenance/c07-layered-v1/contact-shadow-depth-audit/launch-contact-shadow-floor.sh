#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
export XDG_DATA_HOME="$ROOT/c07-static-preview/user-data" CONTACT_SHADOW_OUTPUT="$ROOT/c07-static-preview/contact-shadow-floor"
timeout -k 10 180 "$GODOT_NET" --audio-driver Dummy --resolution 1180x812 --path "$ROOT/c07-static-preview" res://ContactShadowFloorProbe.tscn > "$ROOT/c07-static-preview/contact-shadow-floor-native.log" 2>&1
