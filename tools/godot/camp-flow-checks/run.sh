#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-camp-flow-catalog.XXXXXXXX)"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$WORK/catalog.json"
dotnet run --project tools/godot/camp-flow-checks/CampFlowChecks.csproj -- "$ROOT" "$WORK/catalog.json"
