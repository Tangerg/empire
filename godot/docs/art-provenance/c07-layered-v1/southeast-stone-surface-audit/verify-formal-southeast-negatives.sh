#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
for var in ${!C07_@};do unset "$var";done
export XDG_DATA_HOME=$PWD/c07-static-preview/user-data
for spec in '1 0 background' '0 1 old-material';do
 read -r C07_GATE_BACK_DEPTH C07_GATE_V1_MATERIAL label <<< "$spec";export C07_GATE_BACK_DEPTH C07_GATE_V1_MATERIAL
 set +e;"$GODOT_NET" --headless --path c07-static-preview res://FormalSoutheastGateStoneProbe.tscn > c07-static-preview/formal-southeast-$label-negative.log 2>&1;rc=$?;set -e
 test "$rc" = 1
 if [ "$label" = background ];then expected='Southeast gate actual stone back/front contract failed';else expected='Southeast gate continuous opening or target contract failed';fi
 grep -F "$expected" c07-static-preview/formal-southeast-$label-negative.log
 echo "$label target negative passed"
done
