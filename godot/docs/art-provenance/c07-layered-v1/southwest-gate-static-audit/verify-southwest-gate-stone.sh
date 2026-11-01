#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/southwest-gate-stone-final-build.log 2>&1
export XDG_DATA_HOME=/workspace/scratch/84c9fe948755/c07-static-preview/user-data C07_GATE_V1_MATERIAL=0
for spec in back front roof-fit back-depth; do
 export C07_GATE_FRONT=0 C07_SW_ROOF_FIT=0 C07_GATE_BACK_DEPTH=0
 expected=0
 case $spec in front) export C07_GATE_FRONT=1;; roof-fit) export C07_SW_ROOF_FIT=1;expected=1;; back-depth) export C07_GATE_BACK_DEPTH=1;expected=1;; esac
 set +e
 "$GODOT_NET" --headless --path c07-static-preview res://SouthwestGateStoneProbe.tscn > c07-static-preview/southwest-gate-stone-final-$spec.log 2>&1
 rc=$?
 set -e
 echo "$spec exit=$rc"
 test "$rc" = "$expected"
 case $spec in roof-fit) grep 'ERROR: System.Exception: Southwest gate continuous opening or target contract failed' c07-static-preview/southwest-gate-stone-final-$spec.log;; back-depth) grep 'ERROR: System.Exception: Southwest gate actual stone back/front contract failed' c07-static-preview/southwest-gate-stone-final-$spec.log;; *) grep 'SOUTHWEST_GATE_STONE_STATIC_COMPLETE' c07-static-preview/southwest-gate-stone-final-$spec.log;; esac
 done
