#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "$0")/.." && pwd)
source "$ROOT/empire-c01/tools/godot/env.sh"
P="$ROOT/c07-northeast-formal"
NE_INTERFACE=1 C07_GATE_FRONT=1 "$GODOT_NET" --headless --path "$P" res://FormalNortheastBaselineProbe.tscn > "$P/interface-affine-front.log" 2>&1
"$GODOT_NET" --headless --path "$P" res://FormalNortheastBaselineProbe.tscn > "$P/baseline-final-back.log" 2>&1
for variant in left-post right-beam; do
  set +e
  NE_INTERFACE=1 NE_NEGATIVE="$variant" "$GODOT_NET" --headless --path "$P" res://FormalNortheastBaselineProbe.tscn > "$P/negative-$variant.log" 2>&1
  code=$?
  set -e
  test "$code" = 1
  grep -Fq 'Lookout domain contract failed:' "$P/negative-$variant.log"
  printf 'NEGATIVE_REACHED_FROZEN_HOLE_ASSERTION %s exit=%s\n' "$variant" "$code"
done
