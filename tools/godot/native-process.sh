#!/usr/bin/env bash
# One completion boundary for editor import and native scene processes.
set -euo pipefail
if [[ $# -lt 3 ]]; then
  printf 'Usage: %s LOG LABEL COMMAND [ARG...]\n' "$0" >&2
  exit 2
fi
log="$1"
label="$2"
shift 2
status=0
timeout --signal=TERM --kill-after=15s "${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}" "$@" > "$log" 2>&1 || status=$?
cat "$log"
if [[ "$status" != 0 ]] || grep -Eiq '(^ERROR:|^SCRIPT ERROR:|leaked|segmentation fault|unhandled exception)' "$log"; then
  printf 'Native process/diagnostic gate failed: %s (exit %s); %s\n' "$label" "$status" "$log" >&2
  exit 1
fi
