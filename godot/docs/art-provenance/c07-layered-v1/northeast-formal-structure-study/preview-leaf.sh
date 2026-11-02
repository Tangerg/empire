#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "$0")/.." && pwd)
source "$ROOT/empire-c01/tools/godot/env.sh"
NE_INTERFACE=1 NE_LEAF=1 C07_GATE_FRONT="${1:-0}" NE_DEFAULT_ZOOM="${2:-0}" "$GODOT_NET" --path "$ROOT/c07-northeast-formal" res://FormalNortheastPreview.tscn > "$ROOT/c07-northeast-formal/leaf-preview.log" 2>&1
