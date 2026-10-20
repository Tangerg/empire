#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
frozen=false
verify_guard=false
for option in "$@"; do
  case "$option" in
    --frozen) frozen=true ;;
    --verify-guard) verify_guard=true ;;
    *) echo "Usage: $0 [--frozen] [--verify-guard]" >&2; exit 2 ;;
  esac
done
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-casting-declarations.XXXXXXXX")"
echo "Casting declaration artifacts: $scratch"
cp "$source_dir/Program.cs" "$source_dir/CastingDeclarationChecks.csproj" "$source_dir/NuGet.Config" "$scratch/"
if $frozen; then
  gzip -dc "$root/godot/tests/oracle/casting-declarations.json.gz" > "$scratch/oracle.json"
else
  ./node_modules/.bin/vite-node "$source_dir/oracle.ts" "$scratch/oracle.json"
fi
dotnet build "$scratch/CastingDeclarationChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/CastingDeclarationChecks.dll" "$scratch/oracle.json"
if $verify_guard; then
  python3 "$source_dir/mutants.py" "$root" "$scratch"
fi
