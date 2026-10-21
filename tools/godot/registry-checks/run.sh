#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
if [[ $# -gt 1 || ( $# -eq 1 && $1 != "--frozen" ) ]]; then
  echo "Usage: $0 [--frozen]" >&2
  exit 2
fi
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-registry-checks.XXXXXX)"
FROZEN="$ROOT/godot/tests/oracle/registry-contracts.json.gz"
fixture="$FROZEN"
if [[ ${1:-} != "--frozen" ]]; then
  node node_modules/vite-node/vite-node.mjs tools/godot/registry-checks/oracle.ts "$WORK/oracle.json"
  fixture="$WORK/oracle.json"
fi
timeout --signal=TERM --kill-after=10s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-180}" \
  dotnet build tools/godot/registry-checks/RegistryChecks.csproj \
  --disable-build-servers --nologo -m:1 -p:UseSharedCompilation=false
arguments=("$fixture" "$WORK")
if [[ ${1:-} != "--frozen" ]]; then arguments+=("$FROZEN"); fi
timeout --signal=TERM --kill-after=10s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-180}" \
  dotnet tools/godot/registry-checks/bin/Debug/net8.0/RegistryChecks.dll "${arguments[@]}"
printf 'Registry fixture and diagnostics: %s\n' "$WORK"
