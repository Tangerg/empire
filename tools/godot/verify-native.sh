#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/godot/env.sh
# No installation, publication or user-save mutation. Native checks use fixture sessions.
dotnet build godot/Empire.csproj
dotnet run --project godot/Empire.Core.Tests -- godot/tests/oracle
dotnet run --project tools/godot/application-checks -- .
dotnet run --project tools/godot/persistence-checks/Empire.Persistence.Checks.csproj -- .
"$GODOT_NET" --headless --editor --path godot --import
# Each save-mutating scene owns a fresh temporary namespace. A PASS line before
# native shutdown is not sufficient: check the exit code and teardown diagnostics.
run_scene() {
  local scene="$1" data log status=0
  shift
  data="$(mktemp -d /tmp/empire-native-check-data.XXXXXX)"
  log="$data/native.log"
  XDG_DATA_HOME="$data" "$GODOT_NET" --headless --path godot "res://Scenes/$scene.tscn" "$@" > "$log" 2>&1 || status=$?
  cat "$log"
  if [[ "$status" != 0 ]] || grep -Eiq '(^ERROR:|leaked|segmentation fault|unhandled exception)' "$log"; then
    printf 'Native process/teardown gate failed: %s (exit %s); %s\n' "$scene" "$status" "$log" >&2
    return 1
  fi
}
for scene in PresentationChecks DynamicStateChecks AnimationChecks BattleScreenChecks BattleResultFlowChecks CameraChecks CameraIntegrationChecks LightingLifetimeChecks; do
  run_scene "$scene"
done
run_scene LightingChecks -- --lighting-oracle="$PWD/godot/tests/oracle/lighting.json"
if [[ "${1:-}" == '--full' ]]; then
  # These are development-only comparisons against the preserved original source.
  for suite in feedback staging action-refusal deployment scenario utf16-boundary vision content-validation level-validation persistence-reference progression recruitment transport; do
    bash "tools/godot/$suite-checks/run.sh"
  done
  bash tools/godot/camera-checks/run.sh --native --verify-guard
  bash tools/godot/lighting-checks/run.sh
  bash tools/godot/camp-flow-checks/run.sh
  bash tools/godot/campaign-battle-checks/run.sh
  bash tools/godot/campaign-route-checks/run.sh first
  bash tools/godot/campaign-route-checks/run.sh last
fi
