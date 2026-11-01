#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
for var in ${!C07_@};do unset "$var";done
export C07_STATIC_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_STATIC_RESULT_TAG=selected-council XDG_DATA_HOME="$PWD/c07-static-preview/user-data"
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/selected-council-build.log 2>&1
"$GODOT_NET" --headless --path c07-static-preview res://SelectedAssemblyProbe.tscn > c07-static-preview/selected-council-structure.log 2>&1
for spec in '0 0 roof' '1 0 front' '0 1 local-front';do
 read -r C07_HALL_FRONT C07_HALL_LOCAL_FRONT label <<< "$spec";export C07_HALL_FRONT C07_HALL_LOCAL_FRONT
 "$GODOT_NET" --headless --path c07-static-preview res://CouncilHallCandidateProbe.tscn > c07-static-preview/selected-council-$label.log 2>&1
 echo "$label passed"
done
for spec in '1 0 1 flat' '0 1 0 background';do
 read -r C07_HALL_FLAT_MAX C07_HALL_BACKGROUND C07_HALL_LOCAL_FRONT label <<< "$spec";export C07_HALL_FLAT_MAX C07_HALL_BACKGROUND C07_HALL_LOCAL_FRONT C07_HALL_FRONT=0
 set +e;"$GODOT_NET" --headless --path c07-static-preview res://CouncilHallCandidateProbe.tscn > c07-static-preview/selected-council-$label-negative.log 2>&1;rc=$?;set -e
 test "$rc" = 1;grep -F 'Council hall actual-alpha back/front contract failed' c07-static-preview/selected-council-$label-negative.log
 echo "$label target negative passed"
done
