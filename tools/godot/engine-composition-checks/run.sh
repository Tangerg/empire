#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
FROZEN=false
GUARDS=false
WORK=""
for argument in "$@"; do
  case "$argument" in
    --frozen) FROZEN=true ;;
    --verify-guard) GUARDS=true ;;
    *) if [[ -n "$WORK" ]]; then echo "Unexpected argument: $argument" >&2; exit 2; fi; WORK="$argument" ;;
  esac
done
WORK="${WORK:-$(mktemp -d /tmp/empire-composition.XXXXXX)}"
mkdir -p "$WORK"
if [[ "$FROZEN" == false ]]; then
  node node_modules/typescript/bin/tsc --project tools/godot/engine-composition-checks/tsconfig.json
  for fixture in source-oracle all-capabilities-oracle state-grid-oracle; do
    entry="$fixture"
    [[ "$fixture" == source-oracle ]] && entry=oracle
    node node_modules/vite-node/vite-node.mjs "tools/godot/engine-composition-checks/$entry.ts" "$WORK/$fixture.json.gz"
    cmp "tools/godot/engine-composition-checks/$fixture.json.gz" "$WORK/$fixture.json.gz"
  done
fi
python3 tools/godot/engine-composition-checks/architecture.py "$ROOT"
dotnet run --project tools/godot/engine-composition-checks -- "$ROOT/tools/godot/engine-composition-checks/source-oracle.json.gz" "$ROOT"
if [[ "$GUARDS" == true ]]; then
  python3 tools/godot/engine-composition-checks/mutants.py "$ROOT/tools/godot/engine-composition-checks/source-oracle.json.gz" "$WORK/mutants" "$FROZEN"
fi
