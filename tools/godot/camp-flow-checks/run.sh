#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
dotnet run --project tools/godot/camp-flow-checks/CampFlowChecks.csproj -- "$ROOT"
