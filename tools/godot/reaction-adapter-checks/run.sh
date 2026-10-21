#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
WORK="$(mktemp -d /tmp/empire-reaction-adapter-catalog.XXXXXXXX)"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$WORK/catalog.json"
project="tools/godot/reaction-adapter-checks/ReactionAdapterChecks.csproj"
dotnet build "$project" --configfile tools/godot/reaction-adapter-checks/NuGet.Config --disable-build-servers --nologo
dotnet tools/godot/reaction-adapter-checks/bin/Debug/net8.0/ReactionAdapterChecks.dll godot/tests/oracle/turn-reaction-extensions.json.gz "$WORK/catalog.json"
