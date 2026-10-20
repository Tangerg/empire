#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
work="$(mktemp -d /tmp/empire-board-decoration.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/export-board-decorations.ts "$work/decorations.json"
cmp godot/data/board-decorations.json "$work/decorations.json"
node node_modules/vite-node/vite-node.mjs tools/godot/board-decoration-checks/export-connectors.ts "$work/connectors.json"
gzip -dc godot/tests/oracle/board-connectors.json.gz | cmp - "$work/connectors.json"
dotnet build godot/Empire.csproj --no-restore --nologo
"$GODOT_NET" --headless --path godot res://Scenes/BoardDecorationChecks.tscn > "$work/native.log" 2>&1
"$GODOT_NET" --headless --path godot res://Scenes/ConnectorDecorationChecks.tscn -- "--connectors=$work/connectors.json" > "$work/connectors-native.log" 2>&1
cat "$work/native.log" "$work/connectors-native.log"
if grep -Eiq '(^ERROR:|leaked|segmentation fault|unhandled exception)' "$work/native.log" "$work/connectors-native.log"; then exit 1; fi
printf 'Source board decoration contracts passed. Evidence: %s\n' "$work"
