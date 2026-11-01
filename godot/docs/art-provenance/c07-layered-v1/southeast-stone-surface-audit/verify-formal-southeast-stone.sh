#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
for var in ${!C07_@};do unset "$var";done
export XDG_DATA_HOME=$PWD/c07-static-preview/user-data
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/formal-southeast-probe-build.log 2>&1
for pose in 0 1;do
 C07_GATE_FRONT=$pose "$GODOT_NET" --headless --path c07-static-preview res://FormalSoutheastGateStoneProbe.tscn > c07-static-preview/formal-southeast-stone-$pose.log 2>&1
 echo "stone pose=$pose passed"
done
"$GODOT_NET" --headless --path c07-static-preview res://SoutheastSurfaceEquivalenceProbe.tscn > c07-static-preview/formal-southeast-equivalence.log 2>&1
