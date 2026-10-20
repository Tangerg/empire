#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-tactical-adapter.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/tactical-adapter-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/tactical-adapter-checks -- "$WORK/oracle.json"
python3 tools/godot/tactical-adapter-checks/mutants.py "$WORK/oracle.json"
