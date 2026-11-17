#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"
source tools/godot/env.sh
if [[ $# -ne 1 || "$1" != --write ]]; then
  printf 'Usage: %s --write\n' "$0" >&2
  exit 2
fi
selection="$(mktemp "${TMPDIR:-/tmp}/empire-native-format-sources.XXXXXX")"
trap 'rm -f "$selection"' EXIT
# A failed selector must stop formatting; process substitution hides its exit status.
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" \
  python3 tools/godot/select-native-format-sources.py > "$selection"
mapfile -d '' sources < "$selection"
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" \
  "$DOTNET_ROOT/dotnet" format whitespace --folder . --include "${sources[@]}"
