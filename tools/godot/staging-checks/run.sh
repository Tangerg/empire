#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
timeout_seconds="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
WORK="${1:-$(mktemp -d /tmp/empire-staging.XXXXXX)}"
mkdir -p "$WORK"
gzip -dc godot/tests/oracle/staging.json.gz > "$WORK/oracle.json"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$WORK/catalog.json"
python3 tools/godot/localization-checks/validate_narrative.py "$ROOT"
timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet run --project tools/godot/staging-checks/StagingChecks.csproj -- "$WORK/oracle.json" "$ROOT/godot/data/grayflag-staging.json" "$WORK/catalog.json"
printf 'Staging source comparison passed. Evidence: %s\n' "$WORK"
