#!/usr/bin/env bash
set -euo pipefail
HERE=$(cd -- "$(dirname -- "$0")" && pwd)
for variant in platform-leaf platform-pruned-leaf selected-northwest; do
  NW_VARIANT=$variant NW_DEFAULT_SCALE=1 NW_LOSSLESS_CAPTURE=1 "$GODOT_NET" --audio-driver Dummy --path "$HERE" res://FormalNorthwestPreview.tscn > "$HERE/native-lossless-$variant.log" 2>&1
done
printf 'NORTHWEST_LOSSLESS_THREE_COMPLETE\n'
