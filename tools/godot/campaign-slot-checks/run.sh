#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
scratch="$(mktemp -d /tmp/empire-campaign-slot.XXXXXXXX)"
printf 'Campaign slot verification: %s\n' "$scratch"
node_modules/.bin/vite-node tools/godot/campaign-slot-checks/export-oracle.ts "$scratch/oracle.json"
python3 - "$PWD" "$scratch" <<'PY'
from pathlib import Path
import shutil, sys
root, target = map(Path, sys.argv[1:])
shutil.copytree(root / 'godot/Empire.Core', target / 'Core', ignore=shutil.ignore_patterns('bin', 'obj'))
checks = target / 'checks'
checks.mkdir()
for source in ['godot/Application/CampaignSlotStorage.cs', 'godot/Presentation/CampaignMenu.cs', 'tools/godot/campaign-slot-checks/Program.cs']:
    shutil.copyfile(root / source, checks / Path(source).name)
(checks / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="../Core/Empire.Core.csproj" /></ItemGroup></Project>')
PY
dotnet run --project "$scratch/checks/Checks.csproj" -- "$scratch/oracle.json" "$PWD/godot/data/grayflag-staging.json"
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
  if dotnet run --project "$scratch/mutant/Checks.csproj" -- "$scratch/mutant-input/oracle.json" "$PWD/godot/data/grayflag-staging.json" >"$scratch/mutant.log" 2>&1; then
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
for name in ['CampaignMenu.cs', 'CampaignMenuScreen.cs', 'CampaignMenuChecks.cs']:
    shutil.copyfile(root / 'godot/Presentation' / name, native / 'Presentation' / name)
shutil.copyfile(root / 'godot/Application/CampaignSlotStorage.cs', native / 'CampaignSlotStorage.cs')
shutil.copyfile(root / 'godot/Scenes/CampaignMenuChecks.tscn', native / 'Scenes/CampaignMenuChecks.tscn')
(native / 'MenuNativeChecks.csproj').write_text('<Project Sdk="Godot.NET.Sdk/4.6.3"><PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include="../Core/Empire.Core.csproj" /></ItemGroup></Project>')
(native / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="Campaign menu native checks"\nrun/main_scene="res://Scenes/CampaignMenuChecks.tscn"\nconfig/features=PackedStringArray("4.6", "C#", "GL Compatibility")\n[dotnet]\nproject/assembly_name="MenuNativeChecks"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n')
PY
  dotnet build "$scratch/native/MenuNativeChecks.csproj" --nologo
  XDG_DATA_HOME="$scratch/native-user-data" XDG_CONFIG_HOME="$scratch/native-config" XDG_CACHE_HOME="$scratch/native-cache" "$GODOT_NET" --headless --path "$scratch/native" res://Scenes/CampaignMenuChecks.tscn -- "$scratch/oracle.json" --isolated-menu-art-fixture
fi
