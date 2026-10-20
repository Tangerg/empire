#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-ai-pacing.XXXXXX)}"
mkdir -p "$WORK"
node node_modules/vite-node/vite-node.mjs tools/godot/ai-pacing-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/ai-pacing-checks/AiPacingChecks.csproj -- "$WORK/oracle.json"
printf 'AI source loop comparison passed. Evidence: %s\n' "$WORK"
