#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-hud.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/tactical-hud-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/tactical-hud-checks -- "$WORK/oracle.json"
python3 tools/godot/tactical-hud-checks/mutants.py "$WORK/oracle.json"
