#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/northeast-stone-final-build.log 2>&1
export XDG_DATA_HOME=$PWD/c07-static-preview/user-data
for kind in back front old-material back-depth late-depth; do
 export C07_NE_CASE=$kind C07_GATE_FRONT=0 C07_NE_V1_MATERIAL=0 C07_GATE_BACK_DEPTH=0 C07_NE_LATE_DEPTH=0
 expected=1;target='Northeast gate actual stone back/front contract failed'
 case $kind in back) expected=0;;front) expected=0;export C07_GATE_FRONT=1;;old-material) export C07_NE_V1_MATERIAL=1;target='Northeast gate continuous opening or target contract failed';;back-depth) export C07_GATE_BACK_DEPTH=1;;late-depth) export C07_NE_LATE_DEPTH=1 C07_GATE_FRONT=1;;esac
 set +e
 "$GODOT_NET" --headless --path c07-static-preview res://NortheastGateStoneProbe.tscn > c07-static-preview/northeast-stone-final-$kind.log 2>&1
 rc=$?;set -e;echo "$kind exit=$rc";test "$rc" = "$expected"
 if [ "$expected" = 0 ];then grep NORTHEAST_GATE_STONE_STATIC_COMPLETE c07-static-preview/northeast-stone-final-$kind.log;else grep "ERROR: System.Exception: $target" c07-static-preview/northeast-stone-final-$kind.log;fi
 done
