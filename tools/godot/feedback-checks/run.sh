#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-feedback.XXXXXX)}"
mkdir -p "$WORK"
node node_modules/vite-node/vite-node.mjs tools/godot/feedback-checks/oracle.ts "$WORK/oracle.json"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$WORK/catalog.json"
timeout --signal=TERM --kill-after=15s 180 dotnet build tools/godot/feedback-checks/FeedbackChecks.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
timeout --signal=TERM --kill-after=15s 180 dotnet tools/godot/feedback-checks/bin/Debug/net8.0/FeedbackChecks.dll "$WORK/oracle.json" "$ROOT" "$WORK/catalog.json"
printf 'Historical feedback source comparison passed. Evidence: %s\n' "$WORK"
