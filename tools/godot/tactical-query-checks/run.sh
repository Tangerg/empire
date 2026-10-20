#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-tactical-query.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/tactical-query-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/tactical-query-checks -- "$WORK/oracle.json"
python3 tools/godot/tactical-query-checks/mutants.py "$WORK/oracle.json"
