#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
catalog_work="$(mktemp -d /tmp/empire-campaign-victory-catalog.XXXXXXXX)"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$catalog_work/catalog.json"
fixture="godot/tests/oracle/campaign-victory-opening.jsonl.gz"
if [[ "${1:-}" == "--regenerate" ]]; then
  scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-winning-opening.XXXXXXXX")"
  ./node_modules/.bin/vite-node tools/godot/campaign-victory-checks/oracle.ts "$scratch"
  gzip -dc "$fixture" | cmp - "$scratch/staged-refugees.jsonl"
  fixture="$scratch/staged-refugees.jsonl"
  echo "Byte-identical regenerated source evidence: $fixture"
elif [[ $# -ne 0 ]]; then
  echo "Usage: $0 [--regenerate]" >&2
  exit 2
fi
dotnet run --project tools/godot/campaign-victory-checks -- . "$fixture" "$catalog_work/catalog.json"
