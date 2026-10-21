#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-session.XXXXXX)}"
mkdir -p "$WORK"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$WORK/catalog.json"
node node_modules/vite-node/vite-node.mjs tools/godot/session-contract-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/session-contract-checks/SessionContractChecks.csproj -- "$WORK/oracle.json" "$ROOT" "$WORK/catalog.json"
printf 'Session source oracle and comparison passed. Evidence: %s\n' "$WORK"
