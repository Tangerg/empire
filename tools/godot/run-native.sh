#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/godot/env.sh
if [[ ! -x "$DOTNET_ROOT/dotnet" || ! -x "$GODOT_NET" ]]; then
  echo 'Official .NET 8 and Godot 4.6.3 .NET tools are required. Set EMPIRE_TOOLCHAIN_ROOT; see godot/README.md.' >&2
  exit 1
fi
dotnet build godot/Empire.csproj --nologo
"$GODOT_NET" --headless --editor --path godot --import
exec "$GODOT_NET" --path godot "$@"
