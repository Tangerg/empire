#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-staging.XXXXXX)}"
mkdir -p "$WORK"
node node_modules/vite-node/vite-node.mjs tools/godot/staging-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/staging-checks/StagingChecks.csproj -- "$WORK/oracle.json" "$ROOT/godot/data/grayflag-staging.json"
printf 'Staging source comparison passed. Evidence: %s\n' "$WORK"
