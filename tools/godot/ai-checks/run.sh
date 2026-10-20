#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-ai-checks.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/ai-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/ai-checks/AiChecks.csproj -- "$ROOT/godot" "$WORK/oracle.json" "$WORK"
printf 'Source fixture and diagnostics: %s\n' "$WORK"
