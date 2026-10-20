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
for scene in PresentationChecks DynamicStateChecks AnimationChecks BattleScreenChecks; do
  "$GODOT_NET" --headless --path godot "res://Scenes/$scene.tscn"
done
if [[ "${1:-}" == '--full' ]]; then
  # These are development-only comparisons against the preserved original source.
  for suite in action-refusal deployment scenario utf16-boundary vision content-validation level-validation persistence-reference progression recruitment transport; do
    bash "tools/godot/$suite-checks/run.sh"
  done
  bash tools/godot/campaign-battle-checks/run.sh
  bash tools/godot/campaign-route-checks/run.sh first
  bash tools/godot/campaign-route-checks/run.sh last
fi
