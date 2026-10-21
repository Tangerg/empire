#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
timeout_seconds="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
scratch="$(mktemp -d /tmp/empire-campaign-slot.XXXXXXXX)"
printf 'Campaign slot verification: %s\n' "$scratch"
gzip -dc godot/tests/oracle/campaign-menu.json.gz > "$scratch/oracle.json"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$scratch/catalog.json"
python3 - "$PWD" "$scratch" <<'PY'
from pathlib import Path
import shutil, sys
root, target = map(Path, sys.argv[1:])
shutil.copytree(root / 'godot/Empire.Core', target / 'Core', ignore=shutil.ignore_patterns('bin', 'obj'))
checks = target / 'checks'
checks.mkdir()
for source in ['godot/Application/CampaignSlotStorage.cs', 'godot/Application/SaveFile.cs', 'godot/Presentation/CampaignMenu.cs', 'godot/Localization/GameText.cs', 'tools/godot/campaign-slot-checks/Program.cs']:
    shutil.copyfile(root / source, checks / Path(source).name)
(checks / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="../Core/Empire.Core.csproj" /></ItemGroup></Project>')
PY
timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet run --project "$scratch/checks/Checks.csproj" -- "$scratch/oracle.json" "$PWD/godot/data/grayflag-staging.json" "$scratch/catalog.json"
if [[ " $* " == *" --verify-guard "* ]]; then
  cp -a "$scratch/checks" "$scratch/mutant"
  python3 - "$scratch/mutant/CampaignSlotStorage.cs" <<'PY'
from pathlib import Path
import sys
p = Path(sys.argv[1])
s = p.read_text()
old = 'catch (StoredDocumentException error) { return new(null, error.Message); }'
assert s.count(old) == 1
p.write_text(s.replace(old, 'catch (Exception error) { return new(null, error.Message); }'))
PY
  # A new oracle pathname gives the mutant its own filesystem fixtures, never reuses previous files.
  mkdir "$scratch/mutant-input"
  cp "$scratch/oracle.json" "$scratch/mutant-input/oracle.json"
  if timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet run --project "$scratch/mutant/Checks.csproj" -- "$scratch/mutant-input/oracle.json" "$PWD/godot/data/grayflag-staging.json" "$scratch/catalog.json" >"$scratch/mutant.log" 2>&1; then
    echo 'ERROR: broad-catch mutant escaped detection.' >&2; exit 1
  fi
  grep -q 'definition defects must propagate' "$scratch/mutant.log"
  echo 'Negative control passed: broad-catch masking of definition defects is detected.'
fi
if [[ " $* " == *" --native "* ]]; then
  python3 - "$PWD" "$scratch" <<'PY'
from pathlib import Path
import shutil, sys
root, target = map(Path, sys.argv[1:])
native = target / 'native'
(native / 'Presentation').mkdir(parents=True)
(native / 'Scenes').mkdir()
(native / 'Localization').mkdir()
for name in ['GameText.cs', 'GodotTextCatalog.cs', 'zh_CN.po']:
    shutil.copyfile(root / 'godot/Localization' / name, native / 'Localization' / name)
for name in ['CampaignMenu.cs', 'CampaignMenuScreen.cs', 'CampaignMenuChecks.cs']:
    shutil.copyfile(root / 'godot/Presentation' / name, native / 'Presentation' / name)
shutil.copyfile(root / 'godot/Application/CampaignSlotStorage.cs', native / 'CampaignSlotStorage.cs')
shutil.copyfile(root / 'godot/Application/SaveFile.cs', native / 'SaveFile.cs')
shutil.copyfile(root / 'godot/Scenes/CampaignMenuChecks.tscn', native / 'Scenes/CampaignMenuChecks.tscn')
(native / 'MenuNativeChecks.csproj').write_text('<Project Sdk="Godot.NET.Sdk/4.6.3"><PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="../Core/Empire.Core.csproj" /></ItemGroup></Project>')
(native / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="Campaign menu native checks"\nrun/main_scene="res://Scenes/CampaignMenuChecks.tscn"\nconfig/features=PackedStringArray("4.6", "C#", "GL Compatibility")\n[dotnet]\nproject/assembly_name="MenuNativeChecks"\n[internationalization]\nlocale/translations=PackedStringArray("res://Localization/zh_CN.po")\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n')
PY
  timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet build "$scratch/native/MenuNativeChecks.csproj" --nologo
  export XDG_DATA_HOME="$scratch/native-user-data" XDG_CONFIG_HOME="$scratch/native-config" XDG_CACHE_HOME="$scratch/native-cache"
  timeout --signal=TERM --kill-after=15s "$timeout_seconds" "$GODOT_NET" --headless --editor --path "$scratch/native" --import --quit >"$scratch/import.log" 2>&1
  timeout --signal=TERM --kill-after=15s "$timeout_seconds" "$GODOT_NET" --headless --path "$scratch/native" res://Scenes/CampaignMenuChecks.tscn -- "$scratch/oracle.json" --isolated-menu-art-fixture >"$scratch/native.log" 2>&1
  cat "$scratch/native.log"
  if grep -Eiq "(^ERROR:|leaked|segmentation fault|unhandled exception)" "$scratch/native.log"; then exit 1; fi
fi
