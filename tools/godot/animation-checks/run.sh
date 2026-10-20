#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
dotnet build godot/Empire.csproj --no-restore --nologo
"$GODOT_NET" --headless --editor --path godot --import
"$GODOT_NET" --headless --path godot res://Scenes/AnimationChecks.tscn
