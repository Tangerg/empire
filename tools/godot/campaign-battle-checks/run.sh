#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-campaign-battles.XXXXXX)}"
mkdir -p "$WORK"
node node_modules/vite-node/vite-node.mjs tools/godot/campaign-battle-checks/oracle.ts "$WORK"
dotnet build tools/godot/campaign-battle-checks/CampaignBattleChecks.csproj --nologo -v:minimal
FAILURES=0
for FILE in "$WORK"/*.jsonl; do
  ID="$(basename "$FILE" .jsonl)"
  if ! dotnet run --no-build --project tools/godot/campaign-battle-checks/CampaignBattleChecks.csproj -- "$ROOT/godot" "$FILE" "$WORK"; then
    FAILURES=$((FAILURES + 1))
    FAILURE="$WORK/$ID.native-failure.json"
    if [ -f "$FAILURE" ]; then
      INDEX="$(node -e 'console.log(JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).index)' "$FAILURE")"
      node node_modules/vite-node/vite-node.mjs tools/godot/campaign-battle-checks/source-before.ts "$FILE" "$INDEX" "$WORK/$ID.source-before.json"
    fi
  fi
done
printf 'Completed independent native comparisons; failures=%s. Fixtures and diagnostics: %s\n' "$FAILURES" "$WORK"
test "$FAILURES" -eq 0
