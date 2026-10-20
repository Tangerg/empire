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
if [[ ${1:-} == "--frozen" ]]; then
  dotnet run --project tools/godot/registry-checks/RegistryChecks.csproj -- "$FROZEN" "$WORK"
else
  node node_modules/vite-node/vite-node.mjs tools/godot/registry-checks/oracle.ts "$WORK/oracle.json"
  dotnet run --project tools/godot/registry-checks/RegistryChecks.csproj -- "$WORK/oracle.json" "$WORK" "$FROZEN"
fi
printf 'Registry fixture and diagnostics: %s\n' "$WORK"
