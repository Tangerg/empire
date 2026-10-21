#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
dotnet run --project tools/godot/application-save-checks/ApplicationSaveChecks.csproj -- "$ROOT"

if [[ "${1:-}" == "--verify-guard" ]]; then
  scratch="$(mktemp -d /tmp/empire-application-save-mutant.XXXXXXXX)"
  python3 - "$ROOT" "$scratch" <<'PY_MUTANT'
from pathlib import Path
import shutil, sys
root, target = map(Path, sys.argv[1:])
for directory in ['godot/Empire.Core', 'godot/Presentation/Feedback', 'tools/godot/application-save-checks']:
    shutil.copytree(root / directory, target / directory, ignore=shutil.ignore_patterns('bin', 'obj'))
for source in ['godot/Application/GrayFlagCampaign.cs', 'godot/Application/ApplicationSaveDocument.cs',
               'godot/Application/SaveFile.cs', 'godot/Application/CampaignSlotStorage.cs',
               'godot/Application/GrayFlagSession.cs', 'godot/Presentation/BattlePresentationModels.cs']:
    path = target / source
    path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(root / source, path)
path = target / 'godot/Application/GrayFlagCampaign.cs'
source = path.read_text()
original = 'public JsonObject? RequestSnapshot() => pending?.Request.DeepClone() as JsonObject;'
assert source.count(original) == 1
path.write_text(source.replace(original, 'public JsonObject? RequestSnapshot() => pending?.Request;'))
PY_MUTANT
  if dotnet run --project "$scratch/tools/godot/application-save-checks/ApplicationSaveChecks.csproj" -- "$ROOT" > "$scratch/mutant.log" 2>&1; then
    echo 'ERROR: mutable request ownership escaped detection.' >&2; exit 1
  fi
  grep -q 'A request read model mutated the authoritative pending battle' "$scratch/mutant.log"
  echo 'Negative control passed: escaped mutable request ownership is detected.'
fi
