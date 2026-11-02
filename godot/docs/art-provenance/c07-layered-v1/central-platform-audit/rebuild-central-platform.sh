#!/bin/bash
set -euo pipefail
cd /workspace/scratch/84c9fe948755
source empire-c01/tools/godot/env.sh
python3 c07-static-preview/build-central-platform.py
dotnet build c07-static-preview/Probe.csproj --no-restore
for name in horizontal riser-0 riser-1 riser-2;do
 C07_TEXTURED_SOURCE=res://central-platform-$name-source.json C07_TEXTURED_OUTPUT=res://central-platform-$name-surface.json timeout -k 10 90 "$GODOT_NET" --headless --path c07-static-preview res://TexturedSurfaceExport.tscn
done
python3 c07-static-preview/merge-central-platform-surfaces.py
