#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/godot/env.sh
python3 tools/godot/check-source-metadata.py
CHECK_DATA="$(mktemp -d "${TMPDIR:-/tmp}/empire-native-checks.XXXXXX")"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$CHECK_DATA/catalog.json"
# Bound each native process; a stalled process is a failed gate, never a PASS.
export EMPIRE_CHECK_TIMEOUT_SECONDS="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
dotnet() { command timeout --signal=TERM --kill-after=15s "$EMPIRE_CHECK_TIMEOUT_SECONDS" "$DOTNET_ROOT/dotnet" "$@"; }
export -f dotnet
# No installation, publication or user-save mutation. Native checks use fixture sessions.
dotnet build godot/Empire.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet run --project godot/Empire.Core.Tests -- godot/tests/oracle
# Frozen original-source contracts: these paths require .NET, not a TypeScript runtime.
bash tools/godot/registry-checks/run.sh --frozen
dotnet run --project tools/godot/kernel-contract-checks -- godot/tests/oracle/kernel-contracts.json godot/tests/oracle/kernel-version-contracts.json "$PWD"
bash tools/godot/resource-extension-checks/run.sh --frozen
bash tools/godot/ability-extension-checks/run.sh --frozen
bash tools/godot/spatial-extension-checks/run.sh
bash tools/godot/casting-declaration-checks/run.sh --frozen
bash tools/godot/action-extension-checks/run.sh --frozen
bash tools/godot/engine-composition-checks/run.sh --frozen
bash tools/godot/ai-planning-checks/run.sh --frozen
bash tools/godot/combat-policy-checks/run.sh --frozen
bash tools/godot/departure-rank-checks/run.sh --frozen
bash tools/godot/turn-reaction-extension-checks/run.sh
bash tools/godot/status-extension-checks/run.sh --frozen
bash tools/godot/scenario-extension-checks/run.sh --frozen
bash tools/godot/persistence-reference-checks/run.sh --frozen
bash tools/godot/level-validation-checks/run.sh --frozen
bash tools/godot/reaction-adapter-checks/run.sh
bash tools/godot/catalog-boundary-checks/run.sh
bash tools/godot/application-save-checks/run.sh
dotnet run --project tools/godot/application-checks -- . "$CHECK_DATA/catalog.json"
bash tools/godot/campaign-victory-checks/run.sh
dotnet run --project tools/godot/persistence-checks/Empire.Persistence.Checks.csproj -- .
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
for scene in PresentationChecks DynamicStateChecks AnimationChecks BattleScreenChecks BattleResultFlowChecks CameraChecks CameraIntegrationChecks LightingLifetimeChecks TacticalHudChecks AiPacingIntegrationChecks BoardDecorationChecks LightingRasterParityChecks LightingPerformanceChecks; do
  run_scene "$scene"
done
DOTNET_EnableHWIntrinsic=0 run_scene LightingRasterParityChecks
DOTNET_EnableHWIntrinsic=0 run_scene LightingPerformanceChecks
run_scene SceneryChecks -- --scenery-oracle=res://tests/oracle/board-scenery.json.gz
run_scene ConnectorDecorationChecks -- --connectors="$PWD/godot/tests/oracle/board-connectors.json.gz"
run_scene LightingChecks -- --lighting-oracle="$PWD/godot/tests/oracle/lighting.json"
ORACLE_DATA="$(mktemp -d "${TMPDIR:-/tmp}/empire-native-oracles.XXXXXX")"
for fixture in narrative campaign-menu staging session-contracts tactical-queries tactical-selection tactical-adapter tactical-hud ai-pacing; do
  gzip -dc "godot/tests/oracle/$fixture.json.gz" > "$ORACLE_DATA/$fixture.json"
done
dotnet run --project tools/godot/session-contract-checks/SessionContractChecks.csproj -- "$ORACLE_DATA/session-contracts.json" "$PWD" "$CHECK_DATA/catalog.json"
dotnet run --project tools/godot/tactical-query-checks/TacticalQueryChecks.csproj -- "$ORACLE_DATA/tactical-queries.json"
dotnet run --project tools/godot/tactical-selection-checks/TacticalSelectionChecks.csproj -- "$ORACLE_DATA/tactical-selection.json"
dotnet run --project tools/godot/tactical-adapter-checks/TacticalAdapterChecks.csproj -- "$ORACLE_DATA/tactical-adapter.json" "$CHECK_DATA/catalog.json"
dotnet run --project tools/godot/tactical-hud-checks/TacticalHudChecks.csproj -- "$ORACLE_DATA/tactical-hud.json" "$CHECK_DATA/catalog.json"
dotnet run --project tools/godot/ai-pacing-checks/AiPacingChecks.csproj -- "$ORACLE_DATA/ai-pacing.json"
export EMPIRE_NARRATIVE_ORACLE="$ORACLE_DATA/narrative.json"
run_scene NarrativeChecks
run_scene CampaignMenuChecks -- "$ORACLE_DATA/campaign-menu.json"
run_scene CampaignStagingChecks -- "$ORACLE_DATA/staging.json"
run_scene CampaignNavigationChecks
if [[ "${1:-}" == '--full' ]]; then
  # These are development-only comparisons against the preserved original source.
  bash tools/godot/registry-checks/run.sh
  bash tools/godot/kernel-contract-checks/run.sh
  bash tools/godot/resource-extension-checks/run.sh --verify-guard
  bash tools/godot/ability-extension-checks/run.sh --verify-guard
  bash tools/godot/spatial-extension-checks/run.sh --verify-guard
  bash tools/godot/casting-declaration-checks/run.sh --verify-guard
  bash tools/godot/action-extension-checks/run.sh --verify-guard
  python3 tools/godot/registry-checks/mutants.py
  for suite in feedback staging action-refusal deployment scenario utf16-boundary vision content-validation level-validation persistence-reference progression recruitment transport; do
    bash "tools/godot/$suite-checks/run.sh"
  done
  bash tools/godot/session-contract-checks/run.sh
  bash tools/godot/session-contract-checks/mutants.sh
  bash tools/godot/battle-screen-checks/mutants.sh
  bash tools/godot/tactical-query-checks/run.sh
  bash tools/godot/tactical-selection-checks/run.sh
  python3 tools/godot/tactical-selection-checks/mutants.py
  bash tools/godot/tactical-adapter-checks/run.sh
  bash tools/godot/tactical-hud-checks/run.sh
  python3 tools/godot/tactical-hud-checks/native-mutants.py
  bash tools/godot/ai-pacing-checks/run.sh
  bash tools/godot/ai-pacing-checks/mutants.sh
  bash tools/godot/narrative-checks/run.sh --verify-guard
  bash tools/godot/campaign-slot-checks/run.sh --native --verify-guard
  bash tools/godot/camera-checks/run.sh --native --verify-guard
  bash tools/godot/lighting-checks/run.sh
  bash tools/godot/lighting-checks/lifetime-mutants.sh
  bash tools/godot/board-decoration-checks/run.sh
  python3 tools/godot/board-decoration-checks/mutants.py
  bash tools/godot/scenery-checks/run.sh
  bash tools/godot/scenery-checks/check-mutants.sh
  bash tools/godot/camp-flow-checks/run.sh
  bash tools/godot/campaign-battle-checks/run.sh
  bash tools/godot/campaign-route-checks/run.sh first
  bash tools/godot/campaign-route-checks/run.sh last
fi
