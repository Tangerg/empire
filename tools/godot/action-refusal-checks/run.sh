#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
cd "$root"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-action-refusal.XXXXXXXX")"
echo "Action/refusal artifacts: $scratch"
cp "$source_dir/Program.cs" "$source_dir/ActionRefusalChecks.csproj" "$source_dir/NuGet.Config" "$scratch/"
EMPIRE_ACTION_ORACLE_OUTPUT="$scratch/oracle.json" ./node_modules/.bin/vite-node "$source_dir/oracle.mjs"
dotnet build "$scratch/ActionRefusalChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo "-p:EmpireRoot=$root"
dotnet "$scratch/bin/Debug/net8.0/ActionRefusalChecks.dll" "$scratch/oracle.json"
if [[ "${1:-}" == "--verify-guard" ]]; then
  mkdir "$scratch/ability-mutant" "$scratch/resource-mutant" "$scratch/announcement-mutant"
  cp godot/Empire.Core/Abilities/*.cs "$scratch/ability-mutant/"
  cp godot/Empire.Core/Resources/*.cs "$scratch/resource-mutant/"
  cp godot/Empire.Core/Resources/*.cs "$scratch/announcement-mutant/"
  node - "$scratch" <<'JS'
const fs = require('node:fs'), root = process.argv[2];
const edits = [
  ['announcement-mutant/BattleResources.cs', 'var current = Balance(subject, resource);\n        if (amount == 0 || current is null) return;', 'if (amount == 0) return;\n        var current = Balance(subject, resource);\n        if (current is null) return;'],
  ['ability-mutant/AbilityCommands.cs', 'if (target is null) throw new IllegalActionException(new ActionRefusal("ability.target_required", new() { ["abilityId"] = ability.Id }));', 'if (target is null) throw new IllegalActionException(new ActionRefusal("ability.target_invalid", new() { ["abilityId"] = ability.Id }));'],
  ['resource-mutant/BattleResources.cs', 'var current = Balance(subject, resource);\n        var amount = Normalize(resource, requested);\n        return current is null || current >= amount;', 'return Balance(subject, resource) is not { } current || current >= Normalize(resource, requested);'],
];
for (const [file, before, after] of edits) { const path = `${root}/${file}`, text = fs.readFileSync(path, 'utf8'); if (!text.includes(before)) throw new Error(`Explicit guard target changed: ${file}`); fs.writeFileSync(path, text.replace(before, after)); }
JS
  for mode in ability resource announcement; do
    property=EmpireAbilitiesRoot; expected='attack missing target with no eligible target error'
    if [[ "$mode" == resource ]]; then property=EmpireResourcesRoot; expected='funds error: null'; fi
    if [[ "$mode" == announcement ]]; then property=EmpireResourcesRoot; expected='funds announce error: null'; fi
    dotnet build "$scratch/ActionRefusalChecks.csproj" --configfile "$scratch/NuGet.Config" --disable-build-servers --nologo \
      "-p:EmpireRoot=$root" "-p:$property=$scratch/$mode-mutant" "-p:OutputPath=$scratch/$mode-bin"
    if dotnet "$scratch/$mode-bin/ActionRefusalChecks.dll" "$scratch/oracle.json" >"$scratch/$mode-guard.log" 2>&1; then
      echo "FAIL: $mode semantic mutant escaped its assertion" >&2; exit 1
    fi
    if ! grep -Fq "$expected" "$scratch/$mode-guard.log"; then cat "$scratch/$mode-guard.log" >&2; exit 1; fi
    echo "PASS: $mode deliberate semantic mutant detected by its intended assertion"
  done
fi
