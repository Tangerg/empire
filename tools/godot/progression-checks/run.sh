#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-progression-checks.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/progression-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/progression-checks/ProgressionChecks.csproj -- "$WORK/oracle.json"
printf 'Source fixture and diagnostics: %s\n' "$WORK"
