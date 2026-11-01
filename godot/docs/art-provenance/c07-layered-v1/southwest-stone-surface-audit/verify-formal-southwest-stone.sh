#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
for var in ${!C07_@};do unset "$var";done
export XDG_DATA_HOME=$PWD/c07-static-preview/user-data
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/formal-southwest-final-probe-build.log 2>&1
C07_GATE_FRONT=1 "$GODOT_NET" --headless --path c07-static-preview res://FormalSouthwestGateStoneProbe.tscn > c07-static-preview/formal-southwest-stone-front.log 2>&1
for spec in '1 0 background' '0 1 roof-fit';do
 read -r C07_GATE_BACK_DEPTH C07_SW_ROOF_FIT label <<< "$spec";export C07_GATE_BACK_DEPTH C07_SW_ROOF_FIT
 set +e;"$GODOT_NET" --headless --path c07-static-preview res://FormalSouthwestGateStoneProbe.tscn > c07-static-preview/formal-southwest-$label-negative.log 2>&1;rc=$?;set -e
 test "$rc" = 1
 if [ "$label" = background ];then expected='Southwest gate actual stone back/front contract failed';else expected='Southwest gate continuous opening or target contract failed';fi
 grep -F "$expected" c07-static-preview/formal-southwest-$label-negative.log
done
unset C07_GATE_BACK_DEPTH C07_SW_ROOF_FIT
"$GODOT_NET" --headless --path c07-static-preview res://SouthwestSurfaceEquivalenceProbe.tscn > c07-static-preview/formal-southwest-equivalence.log 2>&1
