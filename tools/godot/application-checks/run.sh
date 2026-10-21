#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-application-catalog.XXXXXXXX)"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$WORK/catalog.json"
dotnet run --project tools/godot/application-checks/ApplicationChecks.csproj -- "$ROOT" "$WORK/catalog.json"
