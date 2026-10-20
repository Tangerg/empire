#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
source "$root/tools/godot/env.sh"
project="${1:-$root/godot}"
project="$(cd "$project" && pwd)"
output="$(mktemp -d /tmp/empire-lighting-exits.XXXXXX)"
printf 'Lighting exit verification output: %s\n' "$output"
dotnet build "$project/Empire.csproj" --nologo --no-restore > "$output/build.log" 2>&1
run() {
  local scene="$1" label="$2" status=0
  shift 2
  "$GODOT_NET" --headless --path "$project" "res://Scenes/$scene.tscn" "$@" > "$output/$label.log" 2>&1 || status=$?
  printf '%s\texit=%s\n' "$label" "$status" | tee -a "$output/exits.tsv"
  # A PASS line is not completion: Godot may report native resource leaks or crash
  # during shutdown afterward. Both the actual process result and diagnostics gate it.
  if [[ "$status" != 0 ]] || grep -Eiq '(^ERROR:|leaked|segmentation fault|unhandled exception)' "$output/$label.log"; then
    cat "$output/$label.log"
    return 1
  fi
  grep -Eq 'passed:|integration passed|PASS [0-9]+ source lighting' "$output/$label.log" || {
    cat "$output/$label.log"; printf 'Missing completion marker: %s\n' "$label"; return 1;
  }
}
for attempt in {1..6}; do run AnimationChecks "animation-$attempt"; done
for attempt in {1..3}; do run LightingLifetimeChecks "lifetime-$attempt"; done
run BattleScreenChecks screen-bindings
run LightingChecks source-contracts -- "--lighting-oracle=$project/tests/oracle/lighting.json"
printf 'PASS clean process exit and diagnostics: 6 animation launches, 3 lifecycle launches, real screen bindings and source contracts.\n'
