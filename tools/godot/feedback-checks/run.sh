#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-feedback.XXXXXX)}"
mkdir -p "$WORK"
node node_modules/vite-node/vite-node.mjs tools/godot/feedback-checks/oracle.ts "$WORK/oracle.json"
dotnet run --project tools/godot/feedback-checks/FeedbackChecks.csproj -- "$WORK/oracle.json" "$ROOT"
printf 'Feedback source oracle and comparison passed. Evidence: %s\n' "$WORK"
