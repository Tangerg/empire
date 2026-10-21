#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
timeout_seconds="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-narrative.XXXXXXXX")"
export XDG_DATA_HOME="$scratch/xdg-data"
printf 'Narrative verification output: %s\n' "$scratch"
gzip -dc godot/tests/oracle/narrative.json.gz > "$scratch/oracle.json"
python3 tools/godot/localization-checks/validate_narrative.py "$PWD"
export EMPIRE_NARRATIVE_ORACLE="$scratch/oracle.json"
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
for name in ['StoryScreen.cs', 'StoryModels.cs', 'NarrativeProjection.cs', 'CampaignShell.cs', 'NarrativeChecks.cs']:
    shutil.copyfile(root / 'godot/Presentation' / name, target / 'Presentation' / name)
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
if [[ " $* " == *" --verify-guard "* ]]; then
  python3 - "$scratch/native/Presentation/NarrativeProjection.cs" <<'PY'
from pathlib import Path
import sys
p = Path(sys.argv[1]); text = p.read_text(); assert text.count('"ui.story.next_beat"') == 1
p.write_text(text.replace('"ui.story.next_beat"', '"ui.story.continue"'))
PY
  timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet build "$scratch/native/NarrativeNativeChecks.csproj" --nologo >"$scratch/mutant-build.log" 2>&1
  if timeout --signal=TERM --kill-after=15s "$timeout_seconds" "$GODOT_NET" --headless --path "$scratch/native" res://Scenes/NarrativeChecks.tscn >"$scratch/mutant.log" 2>&1; then
    echo 'ERROR: narrative label mutant escaped detection.' >&2; exit 1
  fi
  grep -q 'primaryLabel' "$scratch/mutant.log"
  echo 'Negative control passed: frozen rendered expectations detects a wrong next-beat label.'
fi
