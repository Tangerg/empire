#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
WORK="${1:-$(mktemp -d /tmp/empire-ai-pacing-mutants.XXXXXX)}"
mkdir -p "$WORK"
node node_modules/vite-node/vite-node.mjs tools/godot/ai-pacing-checks/oracle.ts "$WORK/oracle.json"
python3 - "$ROOT" "$WORK" <<'PY'
import pathlib, shutil, sys
root, work = map(pathlib.Path, sys.argv[1:])
mutations = {
    'early-normal-pause': ('godot/Application/AiTurnPacer.cs', '? 120 : 90', '? 120 : 89'),
    'early-handoff-pause': ('godot/Application/AiTurnPacer.cs', '? 120 : 90', '? 119 : 90'),
    'initial-pre-delay': ('godot/Application/AiTurnPacer.cs', 'setBusy(true);', 'setBusy(true); resumeAt = nowMilliseconds + 90;'),
    'fallback-metadata': ('godot/Application/GrayFlagSession.cs', 'handsOffTurn(attempted)', 'handsOffTurn(new JsonObject { ["kind"] = "endTurn" })'),
    'swallowed-invariant': ('godot/Application/GrayFlagSession.cs',
        'catch (IllegalActionException error)\n        {\n            onRefusal?.Invoke(error.Refusal);',
        'catch (Exception error) when (error is IllegalActionException or DomainInvariantException)\n        {\n            onRefusal?.Invoke(error is IllegalActionException illegal ? illegal.Refusal : new ActionRefusal("test.refused"));'),
}
for name, (filename, old, new) in mutations.items():
    destination = work / name
    for folder in ['godot/Empire.Core', 'godot/Application', 'godot/Presentation', 'godot/Localization', 'tools/godot/ai-pacing-checks']:
        shutil.copytree(root / folder, destination / folder, ignore=shutil.ignore_patterns('bin', 'obj'), dirs_exist_ok=True)
    target = destination / filename
    source = target.read_text()
    if old not in source: raise RuntimeError(f'Mutation anchor missing: {name}')
    target.write_text(source.replace(old, new))
PY
for mutant in early-normal-pause early-handoff-pause initial-pre-delay fallback-metadata swallowed-invariant; do
    project="$WORK/$mutant/tools/godot/ai-pacing-checks/AiPacingChecks.csproj"
    dotnet build "$project" -v quiet > "$WORK/$mutant-build.log" 2>&1
    if dotnet run --no-build --project "$project" -- "$WORK/oracle.json" "$ROOT" > "$WORK/$mutant.log" 2>&1; then
        printf 'FAIL mutant survived: %s\n' "$mutant" >&2
        exit 1
    fi
    grep -q 'Unhandled exception. System.Exception:' "$WORK/$mutant.log"
    printf 'PASS rejected compiled mutant: %s\n' "$mutant"
done
