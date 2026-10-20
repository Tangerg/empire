#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
node_modules/.bin/vite-node tools/godot/deployment-checks/export-vectors.ts
dotnet run --project tools/godot/deployment-checks/Empire.Deployment.Checks.csproj -- .
