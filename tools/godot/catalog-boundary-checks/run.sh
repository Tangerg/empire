#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
source "$ROOT/tools/godot/env.sh"
cd "$ROOT"
VERIFY_GUARD=false
BENCHMARK=false
for argument in "$@"; do
  case "$argument" in
    --verify-guard) VERIFY_GUARD=true ;;
    --benchmark) BENCHMARK=true ;;
    *) echo "Usage: $0 [--verify-guard] [--benchmark]" >&2; exit 2 ;;
  esac
done
arguments=("$ROOT")
if [[ "$BENCHMARK" == true ]]; then arguments+=(--benchmark); fi
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" \
  dotnet build tools/godot/catalog-boundary-checks --disable-build-servers -m:1 -p:UseSharedCompilation=false
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" \
  dotnet tools/godot/catalog-boundary-checks/bin/Debug/net8.0/CatalogBoundaryChecks.dll "${arguments[@]}"
if [[ "$VERIFY_GUARD" == true ]]; then
  python3 tools/godot/catalog-boundary-checks/mutants.py "$ROOT"
fi
