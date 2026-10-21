#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
FROZEN=0
VERIFY_GUARD=0
FIXTURE=""
for argument in "$@"; do
  case "$argument" in
    --frozen) FROZEN=1 ;;
    --verify-guard) VERIFY_GUARD=1 ;;
    --help)
      echo 'Usage: run.sh [--frozen] [--verify-guard] [fixture.json.gz]'
      exit 0 ;;
    --*) echo "Unknown option: $argument" >&2; exit 2 ;;
    *)
      if [[ -n "$FIXTURE" ]]; then echo 'Only one fixture path may be provided' >&2; exit 2; fi
      FIXTURE="$argument" ;;
  esac
done
if [[ "$FROZEN" == 1 ]]; then
  FIXTURE="${FIXTURE:-$ROOT/godot/tests/oracle/reference-save.json.gz}"
else
  if [[ -z "$FIXTURE" ]]; then
    WORK="$(mktemp -d "${TMPDIR:-/tmp}/empire-reference-save.XXXXXX")"
    FIXTURE="$WORK/reference-save.json.gz"
  fi
  node node_modules/vite-node/vite-node.mjs tools/godot/persistence-reference-checks/oracle.ts "$ROOT/godot/tests/oracle/grayflag-0.58.json" "$FIXTURE"
fi
echo "Reference/save fixture: $FIXTURE"
dotnet run --project tools/godot/persistence-reference-checks/ReferenceChecks.csproj -- "$ROOT" "$FIXTURE"
if [[ "$VERIFY_GUARD" == 1 ]]; then
  python3 tools/godot/persistence-reference-checks/mutation-controls.py "$FIXTURE"
fi
