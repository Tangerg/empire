#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-recruitment.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/recruitment-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/recruitment-checks -- "$WORK/oracle.json"
