#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
for var in ${!C07_@};do unset "$var";done
export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_STATIC_RESULT_TAG=selected
export XDG_DATA_HOME="$PWD/c07-static-preview/user-data"
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/selected-assembly-final-probe-build.log 2>&1
failed=0
for scene in SelectedAssemblyProbe FenceSurfaceProbe FormalCanopyProbe FormalGranaryCanopyProbe FormalGranaryBinProbe FormalGranaryDoorStockProbe;do
 set +e;"$GODOT_NET" --headless --path c07-static-preview res://$scene.tscn > c07-static-preview/selected-assembly-$scene.log 2>&1;rc=$?;set -e
 echo "$scene exit=$rc";if [ "$rc" != 0 ];then failed=1;fi
done
set +e;C07_BIN_WEST=1 C07_STATIC_RESULT_TAG=selected-west "$GODOT_NET" --headless --path c07-static-preview res://FormalGranaryBinProbe.tscn > c07-static-preview/selected-assembly-bin-west.log 2>&1;rc=$?;set -e
echo "bin-west exit=$rc";if [ "$rc" != 0 ];then failed=1;fi
exit "$failed"
