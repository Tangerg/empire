#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
fixture="$(mktemp "${TMPDIR:-/tmp}/empire-vision.XXXXXXXX.json")"
node_modules/.bin/vite-node tools/godot/vision-checks/oracle.ts "$fixture"
dotnet run --project tools/godot/vision-checks -- "$fixture"
