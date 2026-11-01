#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
for var in ${!C07_@};do unset "$var";done
export XDG_DATA_HOME=$PWD/c07-static-preview/user-data
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/formal-southeast-leaf-final-probe-build.log 2>&1
C07_GATE_FRONT=1 "$GODOT_NET" --headless --path c07-static-preview res://FormalSoutheastGateLeafProbe.tscn > c07-static-preview/formal-southeast-leaf-front.log 2>&1
for spec in 'background 0' 'foreground 1';do
 read -r C07_LEAF_NEGATIVE C07_GATE_FRONT <<< "$spec";export C07_LEAF_NEGATIVE C07_GATE_FRONT
 set +e;"$GODOT_NET" --headless --path c07-static-preview res://FormalSoutheastGateLeafProbe.tscn > c07-static-preview/formal-southeast-leaf-$C07_LEAF_NEGATIVE-negative.log 2>&1;rc=$?;set -e
 test "$rc" = 1;grep -F 'Independent wooden leaf actual front/back depth contract failed' c07-static-preview/formal-southeast-leaf-$C07_LEAF_NEGATIVE-negative.log
done
unset C07_LEAF_NEGATIVE C07_GATE_FRONT
"$GODOT_NET" --headless --path c07-static-preview res://SoutheastLeafEquivalenceProbe.tscn > c07-static-preview/formal-southeast-leaf-equivalence.log 2>&1
