#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
source "$ROOT/tools/godot/env.sh"
ORACLE="$(mktemp)"
trap 'rm -f "$ORACLE"' EXIT
cd "$ROOT"
node node_modules/typescript/bin/tsc --noEmit -p tools/godot/tactical-selection-checks/tsconfig.json
node node_modules/vite-node/vite-node.mjs tools/godot/tactical-selection-checks/export-oracle.ts "$ORACLE"
dotnet run --project tools/godot/tactical-selection-checks/TacticalSelectionChecks.csproj -- "$ORACLE"
