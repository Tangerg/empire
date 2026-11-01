#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/formal-surface-static-only-final-build.log 2>&1
for scene in FenceSurfaceEquivalenceProbe SurfaceLegacyScenesProbe;do
 XDG_DATA_HOME=$PWD/c07-static-preview/user-data "$GODOT_NET" --headless --path c07-static-preview res://$scene.tscn > c07-static-preview/formal-surface-static-$scene.log 2>&1
 grep 'COMPLETE' c07-static-preview/formal-surface-static-$scene.log
done
for kind in positive duplicate transparent-material back-depth late-depth; do
 case "$kind" in positive) export C07_FORMAL_FENCE_CASE=''; expected=0; target='C07_FORMAL_FENCE_SURFACE_STATIC_COMPLETE';; duplicate) export C07_FORMAL_FENCE_CASE=$kind; expected=1;target='Duplicate surface primitive';; transparent-material) export C07_FORMAL_FENCE_CASE=$kind; expected=1;target='Opaque surface material includes transparency';; *) export C07_FORMAL_FENCE_CASE=$kind; expected=1;target='Actual back/front/new designed opening alpha contract failed';; esac
 export C07_STATIC_RESULT_TAG=verified-$kind
 set +e; XDG_DATA_HOME=$PWD/c07-static-preview/user-data "$GODOT_NET" --headless --path c07-static-preview res://FenceSurfaceProbe.tscn > c07-static-preview/formal-surface-verified-$kind.log 2>&1; rc=$?;set -e
 echo "$kind exit=$rc expected=$expected";test "$rc" = "$expected";grep -m1 "$target" c07-static-preview/formal-surface-verified-$kind.log
done
