#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-lifecycle-checks.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/lifecycle-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/lifecycle-checks/LifecycleChecks.csproj -- "$ROOT/godot" "$WORK/oracle.json"
printf 'Source fixture and any diagnostics: %s\n' "$WORK"
