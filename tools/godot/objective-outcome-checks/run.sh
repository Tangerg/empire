#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
if [[ $# -ne 0 ]]; then
  printf 'Usage: %s\n' "$0" >&2
  exit 2
fi
work="$(mktemp -d "${TMPDIR:-/tmp}/empire-objective-outcome.XXXXXXXX")"
printf 'Objective/outcome evidence: %s\n' "$work"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$work/catalog.json"
timeout --signal=TERM --kill-after=10s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-180}" \
  dotnet build tools/godot/objective-outcome-checks/ObjectiveOutcomeChecks.csproj \
  --disable-build-servers --nologo -m:1 -p:UseSharedCompilation=false
timeout --signal=TERM --kill-after=10s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-180}" \
  dotnet tools/godot/objective-outcome-checks/bin/Debug/net8.0/ObjectiveOutcomeChecks.dll \
  "$root" "$work/catalog.json" | tee "$work/checks.log"
