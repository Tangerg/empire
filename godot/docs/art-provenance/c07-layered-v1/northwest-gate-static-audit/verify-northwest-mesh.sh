#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
dotnet build c07-static-preview/Probe.csproj --no-restore > c07-static-preview/northwest-mesh-final-build.log 2>&1
export XDG_DATA_HOME=$PWD/c07-static-preview/user-data C07_GATE_BACK_DEPTH=0
for kind in positive uv duplicate duplicate-member back-depth late-depth right-positive left-front-positive right-front-positive right-back-depth right-late-depth; do
 export C07_RIGHT_ROLE=0 C07_DEPTH_OWNER=NWLeftJambSurface C07_MESH_CASE=$kind C07_MESH_UV_MUTANT=0 C07_MESH_DUPLICATE=0 C07_MESH_DUPLICATE_MEMBER=0 C07_SURFACE_DEPTH= C07_GATE_FRONT=0
 expected=1; target='Mapped native draw/pick or unique surface ownership failed'
 case "$kind" in positive) expected=0;; left-front-positive) expected=0; export C07_GATE_FRONT=1;; right-front-positive) expected=0; export C07_GATE_FRONT=1 C07_RIGHT_ROLE=1 C07_DEPTH_OWNER=NWRightJambSurface;; right-positive) expected=0; export C07_RIGHT_ROLE=1 C07_DEPTH_OWNER=NWRightJambSurface;; right-back-depth) export C07_RIGHT_ROLE=1 C07_DEPTH_OWNER=NWRightJambSurface C07_SURFACE_DEPTH=120; target="Northwest gate actual stone back/front contract failed";; right-late-depth) export C07_RIGHT_ROLE=1 C07_DEPTH_OWNER=NWRightJambSurface C07_SURFACE_DEPTH=180 C07_GATE_FRONT=1; target="Northwest gate actual stone back/front contract failed";; uv) export C07_MESH_UV_MUTANT=1;; duplicate) export C07_MESH_DUPLICATE=1;; duplicate-member) export C07_MESH_DUPLICATE_MEMBER=1;; back-depth) export C07_SURFACE_DEPTH=120; target='Northwest gate actual stone back/front contract failed';; late-depth) export C07_SURFACE_DEPTH=180 C07_GATE_FRONT=1; target='Northwest gate actual stone back/front contract failed';; esac
 set +e
 "$GODOT_NET" --headless --path c07-static-preview res://NorthwestMeshProbe.tscn > c07-static-preview/northwest-mesh-final-$kind.log 2>&1
 rc=$?
 set -e
 echo "$kind exit=$rc"; test "$rc" = "$expected"
 if [ "$expected" = 0 ]; then grep 'NORTHWEST_GATE_STONE_STATIC_COMPLETE' c07-static-preview/northwest-mesh-final-$kind.log; else grep "ERROR: System.Exception: $target" c07-static-preview/northwest-mesh-final-$kind.log; fi
 done
