#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
root="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-turn-reaction.XXXXXXXX")"
echo "Turn/reaction check artifacts: $scratch"
cp tools/godot/turn-reaction-extension-checks/{Program.cs,TurnReactionExtensionChecks.csproj,NuGet.Config} "$scratch/"
fixture="$root/godot/tests/oracle/turn-reaction-extensions.json.gz"
if [[ "${1:-}" == "--regenerate" ]]; then
  node node_modules/vite-node/vite-node.mjs tools/godot/turn-reaction-extension-checks/oracle.ts "$scratch/oracle.json.gz"
  fixture="$scratch/oracle.json.gz"
  shift
fi
dotnet build "$scratch/TurnReactionExtensionChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/TurnReactionExtensionChecks.dll" "$fixture"
if [[ "${1:-}" == "--verify-guards" ]]; then
  python3 tools/godot/turn-reaction-extension-checks/mutants.py "$root" "$scratch" "$fixture"
elif [[ $# != 0 ]]; then
  echo "Usage: $0 [--regenerate] [--verify-guards]" >&2
  exit 2
fi
