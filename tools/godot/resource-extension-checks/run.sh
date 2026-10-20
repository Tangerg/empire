#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
frozen=false
guard=false
for argument in "$@"; do
  case "$argument" in
    --frozen) frozen=true ;;
    --verify-guard) guard=true ;;
    *) echo "Usage: $0 [--frozen] [--verify-guard]" >&2; exit 2 ;;
  esac
done
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-resource-extensions.XXXXXXXX")"
echo "Resource extension artifacts: $scratch"
cp "$source_dir/"*.cs "$source_dir/ResourceExtensionChecks.csproj" "$source_dir/NuGet.Config" "$scratch/"
oracle="$scratch/oracle.json"
if [[ "$frozen" == true ]]; then
  oracle="$root/godot/tests/oracle/resource-extensions.json.gz"
else
  ./node_modules/.bin/vite-node "$source_dir/oracle.ts" "$oracle"
fi
dotnet build "$scratch/ResourceExtensionChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/ResourceExtensionChecks.dll" "$oracle"
if [[ "$guard" == true ]]; then
  python3 "$source_dir/mutants.py" "$root" "$scratch" "$oracle"
fi
