#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/southwest-gate-leaf-final-build.log 2>&1
export XDG_DATA_HOME=/workspace/scratch/84c9fe948755/c07-static-preview/user-data C07_GATE_V1_MATERIAL=0 C07_GATE_BACK_DEPTH=0
for spec in back front background late; do
 export C07_LEAF_NEGATIVE= C07_GATE_FRONT=0
 expected=0
 case $spec in front) export C07_GATE_FRONT=1;; background) export C07_LEAF_NEGATIVE=background; expected=1;; late) export C07_GATE_FRONT=1 C07_LEAF_NEGATIVE=late; expected=1;; esac
 set +e
 "$GODOT_NET" --headless --path c07-static-preview res://SouthwestGateLeafProbe.tscn > c07-static-preview/southwest-gate-leaf-final-$spec.log 2>&1
 rc=$?
 set -e
 echo "$spec exit=$rc"
 test "$rc" = "$expected"
 if [ "$expected" = 0 ]; then grep 'SOUTHWEST_GATE_LEAF_STATIC_COMPLETE' c07-static-preview/southwest-gate-leaf-final-$spec.log; else grep 'ERROR: System.Exception: Independent wooden leaf actual front/back depth contract failed' c07-static-preview/southwest-gate-leaf-final-$spec.log; fi
 done
