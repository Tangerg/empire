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
dotnet run --project tools/godot/catalog-boundary-checks -- "${arguments[@]}"
if [[ "$VERIFY_GUARD" == true ]]; then
  python3 tools/godot/catalog-boundary-checks/mutants.py "$ROOT"
fi
