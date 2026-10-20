#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-campaign-route.XXXXXXXX")"
# Keep generated source evidence outside the repository, including failures.
./node_modules/.bin/vite-node tools/godot/campaign-route-checks/oracle.ts "$scratch" "${1:-first}" default
dotnet run --project tools/godot/campaign-route-checks -- . "$scratch/${1:-first}-default.jsonl"
echo "Source route evidence: $scratch"
