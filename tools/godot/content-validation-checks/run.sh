#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
FIXTURE="${1:-/tmp/empire-content-validation.json}"
node node_modules/vite-node/vite-node.mjs tools/godot/content-validation-checks/oracle.ts "$FIXTURE"
dotnet run --project tools/godot/content-validation-checks/ContentChecks.csproj -- "$FIXTURE"
