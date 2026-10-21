#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"
source tools/godot/env.sh
mode="${1:---check}"
case "$mode" in
  --check) options=(--verify-no-changes) ;;
  --write) options=() ;;
  *) printf 'Usage: %s [--check|--write]\n' "$0" >&2; exit 2 ;;
esac
mapfile -d '' sources < <(git ls-files -z --cached --others --exclude-standard -- 'godot/*.cs' 'tools/godot/*.cs' | while IFS= read -r -d '' file; do [[ ! -f "$file" ]] || printf '%s\0' "$file"; done)
if [[ "${#sources[@]}" == 0 ]]; then
  printf 'No native source files found.\n' >&2
  exit 1
fi
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" \
  "$DOTNET_ROOT/dotnet" format whitespace --folder . --include "${sources[@]}" "${options[@]}"
