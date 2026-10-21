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
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-combat-policies.XXXXXXXX")"
echo "Combat policy artifacts: $scratch"
cp "$source_dir/"*.cs "$source_dir/CombatPolicyChecks.csproj" "$source_dir/NuGet.Config" "$scratch/"
oracle="$scratch/oracle.json"
if [[ "$frozen" == true ]]; then
  cp "$root/godot/tests/oracle/combat-policies.json.gz" "$scratch/oracle.json.gz"
  oracle="$scratch/oracle.json.gz"
else
  ./node_modules/.bin/vite-node "$source_dir/oracle.ts" "$oracle"
fi
dotnet build "$scratch/CombatPolicyChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/CombatPolicyChecks.dll" "$oracle"
if [[ "$guard" == true ]]; then
  python3 "$source_dir/mutants.py" "$root" "$scratch" "$oracle"
fi
