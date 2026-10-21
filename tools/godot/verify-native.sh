#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/godot/env.sh
if [[ $# -ne 0 ]]; then
  printf "Usage: %s\n" "$0" >&2
  exit 2
fi
python3 tools/godot/check-source-metadata.py
# Bound each native process; a stalled process is a failed gate, never a PASS.
export EMPIRE_CHECK_TIMEOUT_SECONDS="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
dotnet() { command timeout --signal=TERM --kill-after=15s "$EMPIRE_CHECK_TIMEOUT_SECONDS" "$DOTNET_ROOT/dotnet" "$@"; }
export -f dotnet
# No installation, publication or user-save mutation. Native checks use fixture sessions.
dotnet build godot/Empire.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet build godot/Empire.Core.Tests --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet godot/Empire.Core.Tests/bin/Debug/net8.0/Empire.Core.Tests.dll godot/tests/oracle
# Native schema 2/3 intentionally rejects the old prose-bearing source inputs.
# Historical suites remain development evidence, not an implicit current-schema gate.
bash tools/godot/objective-outcome-checks/run.sh
bash tools/godot/catalog-boundary-checks/run.sh
timeout --signal=TERM --kill-after=15s "$EMPIRE_CHECK_TIMEOUT_SECONDS" "$GODOT_NET" --headless --editor --path godot --import
# Each save-mutating scene owns a fresh temporary namespace. A PASS line before
# native shutdown is not sufficient: check the exit code and teardown diagnostics.
run_scene() {
  local scene="$1" data log status=0
  shift
  data="$(mktemp -d "${TMPDIR:-/tmp}/empire-native-check-data.XXXXXX")"
  log="$data/native.log"
  XDG_DATA_HOME="$data" timeout --signal=TERM --kill-after=15s "$EMPIRE_CHECK_TIMEOUT_SECONDS" "$GODOT_NET" --headless --path godot "res://Scenes/$scene.tscn" "$@" > "$log" 2>&1 || status=$?
  cat "$log"
  if [[ "$status" != 0 ]] || grep -Eiq '(^ERROR:|leaked|segmentation fault|unhandled exception)' "$log"; then
    printf 'Native process/teardown gate failed: %s (exit %s); %s\n' "$scene" "$status" "$log" >&2
    return 1
  fi
}
for scene in BattleScreenChecks BattleResultFlowChecks CampaignNavigationChecks; do
  run_scene "$scene"
done
printf 'Current native contracts, replay/save determinism, and application flow passed. Manual GUI and export acceptance are separate.\n'
