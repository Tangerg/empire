#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
source "$root/tools/godot/env.sh"
project="$(cd "${1:-$root/godot}" && pwd)"
output="$(mktemp -d /tmp/empire-lighting-performance.XXXXXX)"
printf 'Lighting performance verification output: %s\n' "$output"
dotnet build "$project/Empire.csproj" --nologo --no-restore > "$output/build.log" 2>&1 || { cat "$output/build.log"; exit 1; }
status=0
"$GODOT_NET" --headless --path "$project" res://Scenes/LightingPerformanceChecks.tscn > "$output/checks.log" 2>&1 || status=$?
cat "$output/checks.log"
printf 'Process exit: %s\n' "$status"
if [[ "$status" != 0 ]] || grep -Eiq '(^ERROR:|leaked|segmentation fault|unhandled exception)' "$output/checks.log"; then exit 1; fi
grep -q 'Lighting performance checks passed:' "$output/checks.log"
