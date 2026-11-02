#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "$0")/.." && pwd)
source "$ROOT/empire-c01/tools/godot/env.sh"
P="$ROOT/c07-northeast-formal"
dotnet build "$P/Probe.csproj" --no-restore > "$P/whole-gate-final-build.log" 2>&1
for front in 0 1; do
 NE_INTERFACE=1 NE_LEAF=1 C07_GATE_FRONT="$front" "$GODOT_NET" --headless --path "$P" res://FormalNortheastBaselineProbe.tscn > "$P/whole-gate-$front.log" 2>&1
done
"$GODOT_NET" --headless --path "$P" res://FormalNortheastLeafProbe.tscn > "$P/leaf-closed-probe.log" 2>&1
for variant in early-depth late-depth road-invasion flat-foot; do
 set +e
 NE_LEAF_NEGATIVE="$variant" "$GODOT_NET" --headless --path "$P" res://FormalNortheastLeafProbe.tscn > "$P/negative-leaf-$variant.log" 2>&1
 code=$?
 set -e
 test "$code" = 1
 case "$variant" in
 road-invasion) pattern='Leaf enters original road:';;
 flat-foot) pattern='Exported leaf support deviates from its observed sloping foot.';;
 *) pattern='Independent leaf front/back Pick contract failed.';;
 esac
 grep -Fq "$pattern" "$P/negative-leaf-$variant.log"
 printf 'EXPECTED_CONTRACT_FAILURE %s %s\n' "$variant" "$pattern"
done
