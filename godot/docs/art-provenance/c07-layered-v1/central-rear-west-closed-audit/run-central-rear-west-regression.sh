#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
P="$ROOT/c07-static-preview"
run(){ n="$1"; shift; env "$@" timeout -k 5 150 "$GODOT_NET" --headless --path "$P" "res://$n.tscn" > "$P/central-rear-west-regression-$n.log" 2>&1; echo "$n passed"; }
run SelectedAssemblyProbe & a=$!
run CentralPlatformProbe C07_PLATFORM_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_PLATFORM_OUTPUT=res://central-rear-west-regression-platform.json & b=$!
wait "$a"; wait "$b"
run CentralLandmarkProbe C07_CENTRAL_OUTPUT=res://central-rear-west-regression-core.json & a=$!
run CentralSouthAssemblyOwnershipProbe C07_SOUTH_BEARING_SCENE=res://data/scenes/c01-07-selected-assembly.json C07_SOUTH_ASSEMBLY_OUTPUT=res://central-rear-west-regression-south.json & b=$!
wait "$a"; wait "$b"
run CentralSelectedResponsibilityProbe
