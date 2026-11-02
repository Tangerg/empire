#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "$0")/.." && pwd)
source "$ROOT/empire-c01/tools/godot/env.sh"
NE_INTERFACE=1 NE_LEAF=1 NE_FULL_SCENE=1 NE_SELECTED=1 NE_SELECTED_CAPTURE=1 "$GODOT_NET" --audio-driver Dummy --path "$ROOT/c07-northeast-formal" res://FormalNortheastPreview.tscn > "$ROOT/c07-northeast-formal/selected-native-capture.log" 2>&1
