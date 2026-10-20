#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-kernel.XXXXXX)}"
mkdir -p "$WORK"
node node_modules/typescript/bin/tsc --project tools/godot/kernel-contract-checks/tsconfig.json
node node_modules/vite-node/vite-node.mjs tools/godot/kernel-contract-checks/oracle.ts "$WORK/oracle.json"
node node_modules/vite-node/vite-node.mjs tools/godot/kernel-contract-checks/version-oracle.ts "$WORK/versions.json"
cmp godot/tests/oracle/kernel-version-contracts.json "$WORK/versions.json"
cmp godot/tests/oracle/kernel-contracts.json "$WORK/oracle.json"
dotnet run --project tools/godot/kernel-contract-checks -- "$WORK/oracle.json" "$WORK/versions.json" "$ROOT"
python3 tools/godot/kernel-contract-checks/mutants.py "$WORK/oracle.json" "$WORK/mutations" "$WORK/versions.json"
printf 'Kernel source oracle, frozen fixture, native comparison, and mutation controls passed. Evidence: %s\n' "$WORK"
