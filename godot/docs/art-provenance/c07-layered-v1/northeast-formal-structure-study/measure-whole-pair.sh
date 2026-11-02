#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "$0")/.." && pwd)
source "$ROOT/empire-c01/tools/godot/env.sh"
for baseline in 1 0; do
 NE_INTERFACE=1 NE_LEAF=1 NE_FULL_SCENE=1 NE_MEASURE=1 NE_COST_BASELINE="$baseline" "$GODOT_NET" --audio-driver Dummy --path "$ROOT/c07-northeast-formal" res://FormalNortheastPreview.tscn > "$ROOT/c07-northeast-formal/whole-cost-$baseline.log" 2>&1
done
