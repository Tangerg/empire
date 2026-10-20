#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
frozen=false
guard=false
for argument in "$@"; do
  case "$argument" in
    --frozen) frozen=true ;;
    --verify-guard) guard=true ;;
    *) echo "Usage: $0 [--frozen] [--verify-guard]" >&2; exit 2 ;;
  esac
done
WORK="$(mktemp -d "${TMPDIR:-/tmp}/empire-action-extensions.XXXXXX")"
FIXTURE="$PWD/godot/tests/oracle/action-extensions.json.gz"
if [[ "$frozen" != true ]]; then
  node node_modules/vite-node/vite-node.mjs tools/godot/action-extension-checks/oracle.ts "$WORK/oracle.json"
  gzip -dc "$FIXTURE" > "$WORK/frozen.json"
  cmp "$WORK/oracle.json" "$WORK/frozen.json"
  FIXTURE="$WORK/oracle.json"
fi
dotnet run --project tools/godot/action-extension-checks -- "$FIXTURE"
if [[ "$guard" == true ]]; then
  python3 tools/godot/action-extension-checks/mutants.py "$FIXTURE"
fi
