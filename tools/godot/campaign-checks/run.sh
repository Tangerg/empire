#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-campaign-checks.XXXXXX)"
node node_modules/vite-node/vite-node.mjs tools/godot/campaign-checks/oracle.ts "$WORK/oracle.json"
node node_modules/vite-node/vite-node.mjs tools/godot/campaign-checks/validation-oracle.ts "$WORK/validation.json"
node node_modules/vite-node/vite-node.mjs tools/godot/campaign-checks/rules-oracle.ts "$WORK/rules.json"
dotnet run --project tools/godot/campaign-checks/CampaignChecks.csproj -- "$ROOT/godot" "$WORK/oracle.json" "$WORK/validation.json" "$WORK" "$WORK/rules.json"
printf 'Source fixtures and diagnostics: %s\n' "$WORK"
