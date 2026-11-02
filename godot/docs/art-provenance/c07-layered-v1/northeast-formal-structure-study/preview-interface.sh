#!/usr/bin/env bash
set -euo pipefail
HERE=$(cd -- "$(dirname -- "$0")" && pwd)
NE_INTERFACE=${NE_INTERFACE:-1} "$GODOT_NET" --audio-driver Dummy --path "$HERE" res://FormalNortheastPreview.tscn > "$HERE/interface-native-preview.log" 2>&1
