#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-session-mutants.XXXXXX)}"
mkdir -p "$WORK"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$WORK/catalog.json"
node node_modules/vite-node/vite-node.mjs tools/godot/session-contract-checks/oracle.ts "$WORK/oracle.json"
python3 - "$ROOT" "$WORK" <<'PY'
import pathlib, shutil, sys
root, work = map(pathlib.Path, sys.argv[1:])
mutations = {
    'flattened-refusal': ('godot/Application/GrayFlagSession.cs', 'refusals.Describe(error.Refusal)', 'error.Message'),
    'hardcoded-handoff': ('godot/Empire.Core/Transactions/Battle.cs', 'registration.HandsOffTurn', '(kind is "endTurn" or "finishDeployment")'),
    'restore-noop': ('godot/Empire.Core/Transactions/Battle.cs', '_state = restored;', '_state = Snapshot();'),
    'lost-event-truncation': ('godot/Application/GrayFlagSession.cs', 'events.RemoveRange(checkpoint.EventCount, events.Count - checkpoint.EventCount);', '// mutant: semantic event history not truncated'),
    'stale-undo': ('godot/Application/GrayFlagSession.cs', 'if (expectedRevision != revision) return new(false, text.Format("error.session.revision_changed"));', '// mutant: stale revision accepted'),
}
for name, (filename, old, new) in mutations.items():
    destination = work / name
    for folder in ['godot/Empire.Core', 'godot/Application', 'godot/Presentation', 'tools/godot/session-contract-checks', 'tools/godot/check-support', 'godot/Localization']:
        shutil.copytree(root / folder, destination / folder, ignore=shutil.ignore_patterns('bin', 'obj'), dirs_exist_ok=True)
    target = destination / filename
    source = target.read_text()
    if old not in source: raise RuntimeError(f'Mutation anchor missing: {name}')
    target.write_text(source.replace(old, new))
PY
for mutant in flattened-refusal hardcoded-handoff restore-noop lost-event-truncation stale-undo; do
    project="$WORK/$mutant/tools/godot/session-contract-checks/SessionContractChecks.csproj"
    dotnet build "$project" -v quiet > "$WORK/$mutant-build.log" 2>&1
    if dotnet run --no-build --project "$project" -- "$WORK/oracle.json" "$ROOT" "$WORK/catalog.json" > "$WORK/$mutant.log" 2>&1; then
        printf 'FAIL mutant survived: %s\n' "$mutant" >&2
        exit 1
    fi
    grep -q 'Unhandled exception. System.Exception:' "$WORK/$mutant.log"
    printf 'PASS rejected compiled mutant: %s\n' "$mutant"
done
