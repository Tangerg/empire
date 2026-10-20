#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
dotnet run --project tools/godot/persistence-reference-checks/ReferenceChecks.csproj -- "$ROOT"
