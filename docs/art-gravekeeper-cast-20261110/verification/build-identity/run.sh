#!/usr/bin/env bash
set -euo pipefail
# Usage: DOTNET=/absolute/path/to/dotnet ./run.sh OLD_DIR NEW_DIR OUTPUT_DIR
cd "$(dirname "$0")"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/.cli}"
export HOME="${HOME:-$PWD/.home}"
export TMPDIR="$PWD/.tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "$TMPDIR" "$DOTNET_CLI_HOME"
"${DOTNET:-dotnet}" run --project Audit.csproj -- "$@"
