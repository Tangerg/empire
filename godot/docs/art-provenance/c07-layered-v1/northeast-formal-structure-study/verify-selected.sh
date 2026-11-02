#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "$0")/.." && pwd)
source "$ROOT/empire-c01/tools/godot/env.sh"
P="$ROOT/c07-northeast-formal"
dotnet build "$ROOT/empire-c01/godot/Empire.csproj" --no-restore -t:Rebuild > "$P/selected-production-rebuild.log" 2>&1
dotnet build "$P/Probe.csproj" --no-restore > "$P/selected-probe-build.log" 2>&1
for probe in SelectedAssemblyProbe SelectedResponsibilityProbe FormalNortheastLeafProbe; do
 NE_SELECTED=1 "$GODOT_NET" --headless --path "$P" "res://$probe.tscn" > "$P/selected-$probe.log" 2>&1
done
for front in 0 1; do
 NE_INTERFACE=1 NE_LEAF=1 NE_SELECTED=1 C07_GATE_FRONT="$front" "$GODOT_NET" --headless --path "$P" res://FormalNortheastBaselineProbe.tscn > "$P/selected-gate-$front.log" 2>&1
done
