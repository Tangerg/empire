#!/bin/bash
set -euo pipefail
ROOT=/workspace/scratch/84c9fe948755
source "$ROOT/empire-c01/tools/godot/env.sh"
for part in cloth west-post front-post right-post rear-post;do
 C07_TEXTURED_SOURCE="res://southern-supply-canopy-$part-source.json" C07_TEXTURED_OUTPUT="res://data/scenes/c01-07-southern-supply-canopy-$part-surface.json" timeout -k 10 60 "$GODOT_NET" --headless --path "$ROOT/c07-static-preview" res://TexturedSurfaceExport.tscn > "$ROOT/c07-static-preview/southern-supply-canopy-$part-export.log" 2>&1
 tail -1 "$ROOT/c07-static-preview/southern-supply-canopy-$part-export.log"
done
