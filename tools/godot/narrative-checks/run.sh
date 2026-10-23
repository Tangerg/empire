#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
timeout_seconds="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-narrative.XXXXXXXX")"
export XDG_DATA_HOME="$scratch/xdg-data" XDG_CONFIG_HOME="$scratch/xdg-config" XDG_CACHE_HOME="$scratch/xdg-cache"
export XDG_RUNTIME_DIR="$scratch/xdg-runtime"
mkdir -m 700 "$XDG_RUNTIME_DIR"
printf 'Narrative verification output: %s\n' "$scratch"
cp godot/tests/native/narrative.json "$scratch/expectations.json"
python3 tools/godot/localization-checks/validate_narrative.py "$PWD"
export EMPIRE_NARRATIVE_EXPECTATIONS="$scratch/expectations.json"
python3 - "$PWD" "$scratch/native" <<'PY'
from pathlib import Path
import hashlib, json, shutil, sys
root, target = map(Path, sys.argv[1:])
(target / 'Presentation').mkdir(parents=True)
(target / 'Scenes').mkdir()
(target / 'data').mkdir()
(target / 'Localization').mkdir()
for name in ['GameText.cs', 'GodotTextCatalog.cs', 'zh_CN.po']:
    shutil.copyfile(root / 'godot/Localization' / name, target / 'Localization' / name)
for name in ['CharacterPortrait.cs', 'StoryScreen.cs', 'StoryModels.cs', 'NarrativeProjection.cs', 'CampaignShell.cs', 'NarrativeChecks.cs']:
    shutil.copyfile(root / 'godot/Presentation' / name, target / 'Presentation' / name)
shutil.copyfile(root / 'godot/Presentation/Feedback/IdentityNames.cs', target / 'Presentation/IdentityNames.cs')
# Copy the exact production loader, without unrelated board manifest types.
loader = (root / 'godot/Presentation/ManifestModels.cs').read_text()
(target / 'Presentation/PresentationJson.cs').write_text('using System.Text.Json;\nnamespace Empire.Presentation;\n' + loader[loader.index('public static class PresentationJson'):])
shutil.copyfile(root / 'godot/Scenes/NarrativeChecks.tscn', target / 'Scenes/NarrativeChecks.tscn')
for name in ['grayflag-story.json', 'grayflag-staging.json']:
    shutil.copyfile(root / 'godot/data' / name, target / 'data' / name)
metadata = json.loads((target / 'data/grayflag-staging.json').read_text())
assets = list(metadata['portraits'].values()) + list(metadata['storyScenes'].values()) + [chapter['art'] for chapter in metadata['chapters'].values()]
for art in assets:
    source = root / art['source'].lstrip('/')
    resource = Path(art['resourcePath'].removeprefix('res://'))
    exported = root / 'godot' / resource
    digest = hashlib.sha256(exported.read_bytes()).hexdigest()
    assert digest == art['sha256'] == hashlib.sha256(source.read_bytes()).hexdigest(), f'Source/export art mismatch: {source}'
    (target / resource).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(exported, target / resource)
(target / 'NarrativeNativeChecks.csproj').write_text('<Project Sdk="Godot.NET.Sdk/4.6.3"><PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="' + str(root / 'godot/Empire.Core/Empire.Core.csproj') + '" /></ItemGroup></Project>')
(target / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="Narrative native checks"\nrun/main_scene="res://Scenes/NarrativeChecks.tscn"\nconfig/features=PackedStringArray("4.6", "C#", "GL Compatibility")\n[display]\nwindow/size/viewport_width=1280\nwindow/size/viewport_height=720\n[dotnet]\nproject/assembly_name="NarrativeNativeChecks"\n[internationalization]\nlocale/translations=PackedStringArray("res://Localization/zh_CN.po")\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n')
print(f'Verified {len(assets)} original source/export asset references (including repeated chapter images).')
PY
timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet build "$scratch/native/NarrativeNativeChecks.csproj" --nologo
timeout --signal=TERM --kill-after=15s 300 "$GODOT_NET" --headless --editor --path "$scratch/native" --import --quit >"$scratch/import.log" 2>&1
timeout --signal=TERM --kill-after=15s 300 "$GODOT_NET" --headless --path "$scratch/native" res://Scenes/NarrativeChecks.tscn > "$scratch/checks.log" 2>&1
cat "$scratch/checks.log"
if grep -Eiq "(^ERROR:|leaked|segmentation fault|unhandled exception)" "$scratch/checks.log"; then exit 1; fi
expect_narrative_failure() {
  local name="$1" log="$2" status=0
  shift 2
  "$@" >"$log" 2>&1 || status=$?
  if [[ "$status" != 1 ]] ||
    ! grep -Fq 'ERROR: System.AggregateException: Native narrative check groups failed.' "$log" ||
    grep -Eiq 'leaked|segmentation fault|unhandled exception|core dumped|fatal error|fatal signal|crashhandler|program crashed' "$log"; then
    printf 'ERROR: narrative %s did not exit with a clean assertion failure (exit %s); %s\n' "$name" "$status" "$log" >&2
    return 1
  fi
  printf 'Negative process gate passed: %s (exit 1, expected assertion failure, clean teardown).\n' "$name"
}
if [[ " $* " == *" --verify-guard "* ]]; then
  for mutation in prose identity; do
    python3 - "$scratch/expectations.json" "$scratch/$mutation.json" "$mutation" <<'PYTHON'
from pathlib import Path
import json, sys
source, target, mutation = sys.argv[1:]
fixture = json.loads(Path(source).read_text())
if mutation == 'prose':
    case = next(case for case in fixture['storyCases'] if case['presentationId'] == 'c01/brief-03' and case['beatIndex'] == 1 and not case['ending'])
    case['expected']['text'] = 'Incorrect reinforcement deadline expectation'
else:
    fixture['sourceDocuments']['speakers']['roderick'] = 'Incorrect speaker identity expectation'
Path(target).write_text(json.dumps(fixture, ensure_ascii=False))
PYTHON
    expect_narrative_failure "$mutation" "$scratch/$mutation-mutant.log" \
      env EMPIRE_NARRATIVE_EXPECTATIONS="$scratch/$mutation.json" timeout --signal=TERM --kill-after=15s "$timeout_seconds" "$GODOT_NET" --headless --path "$scratch/native" res://Scenes/NarrativeChecks.tscn
  done
  grep -q 'Narrative group passed: CheckIdentityProjections' "$scratch/prose-mutant.log"
  grep -q 'Narrative group failed: CheckNativeProjections' "$scratch/prose-mutant.log"
  grep -q 'Narrative group failed: CheckNativeWidgets' "$scratch/prose-mutant.log"
  grep -q 'Narrative group failed: CheckIdentityProjections' "$scratch/identity-mutant.log"
  grep -q 'Narrative group passed: CheckNativeProjections' "$scratch/identity-mutant.log"
  echo 'Negative controls passed: native prose/widgets and independent shared identities reject incorrect expectations.'
  python3 - "$scratch/native/Presentation/NarrativeProjection.cs" <<'PY'
from pathlib import Path
import sys
p = Path(sys.argv[1]); text = p.read_text(); assert text.count('"ui.story.next_beat"') == 1
p.write_text(text.replace('"ui.story.next_beat"', '"ui.story.continue"'))
PY
  timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet build "$scratch/native/NarrativeNativeChecks.csproj" --nologo >"$scratch/mutant-build.log" 2>&1
  expect_narrative_failure next-beat "$scratch/mutant.log" \
    timeout --signal=TERM --kill-after=15s "$timeout_seconds" "$GODOT_NET" --headless --path "$scratch/native" res://Scenes/NarrativeChecks.tscn
  grep -q 'primaryLabel' "$scratch/mutant.log"
  echo 'Negative control passed: reviewed native expectations detect a wrong next-beat label.'
fi
