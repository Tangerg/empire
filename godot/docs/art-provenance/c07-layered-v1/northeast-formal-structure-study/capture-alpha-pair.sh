#!/usr/bin/env bash
set -euo pipefail
HERE=$(cd -- "$(dirname -- "$0")" && pwd)
for variant in 0 1; do
  NE_INTERFACE=$variant NE_ALPHA_CAPTURE=1 "$GODOT_NET" --audio-driver Dummy --path "$HERE" res://FormalNortheastPreview.tscn > "$HERE/native-alpha-$variant.log" 2>&1
done
printf 'NATIVE_ISOLATED_ALPHA_PAIR_COMPLETE\n'
