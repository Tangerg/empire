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
selection="$(mktemp "${TMPDIR:-/tmp}/empire-native-format-sources.XXXXXX")"
trap 'rm -f "$selection"' EXIT
# A failed selector must stop the gate; process substitution hides its exit status.
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" \
  python3 tools/godot/select-native-format-sources.py > "$selection"
mapfile -d '' sources < "$selection"
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" \
  "$DOTNET_ROOT/dotnet" format whitespace --folder . --include "${sources[@]}" "${options[@]}"
