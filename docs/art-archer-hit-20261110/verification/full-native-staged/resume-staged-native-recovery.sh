#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then
  printf 'Usage: %s REPOSITORY RECOVERY_DIRECTORY\n' "$0" >&2
  exit 2
fi
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
python3 "$here/audit-staged-recovery.py" --run "$2"
exec python3 -u "$here/staged-native-recovery.py" --repo "$1" --output-dir "$2"
