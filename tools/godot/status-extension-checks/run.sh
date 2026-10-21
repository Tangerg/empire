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
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-status-extensions.XXXXXXXX")"
export EMPIRE_TEST_ARTIFACTS="$scratch"
echo "Status extension artifacts: $scratch"
if [[ "$frozen" == true ]]; then
  oracle="$root/godot/tests/oracle/status-extensions.json.gz"
else
  oracle="$scratch/oracle.json"
  ./node_modules/.bin/vite-node "$source_dir/oracle.ts" "$oracle"
fi
dotnet run --project "$source_dir/StatusExtensionChecks.csproj" -- "$oracle"
if [[ "$guard" == true ]]; then
  python3 "$source_dir/mutants.py" "$oracle"
fi
