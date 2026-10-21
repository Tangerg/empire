#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-camera.XXXXXXXX")"
printf 'Camera verification output: %s\n' "$scratch"
EMPIRE_CAMERA_ORACLE_OUTPUT="$scratch/oracle.json" node_modules/.bin/vite-node tools/godot/camera-checks/export-oracle.ts
dotnet run --project tools/godot/camera-checks/CameraChecks.csproj -- "$scratch/oracle.json"
if [[ " $* " == *" --verify-guard "* ]]; then
  python3 - "$PWD" "$scratch/mutant" <<'PY'
from pathlib import Path
import sys
root, target = map(Path, sys.argv[1:])
target.mkdir()
for name in ['BattlefieldCamera.cs', 'BattlefieldGestures.cs', 'BattlefieldFocus.cs', 'BattlePresentationModels.cs']:
    text = (root / 'godot/Presentation' / name).read_text()
    if name == 'BattlefieldGestures.cs':
        assert text.count('>= 6)') == 1
        text = text.replace('>= 6)', '> 6)')
    (target / name).write_text(text)
(target / 'Program.cs').write_text((root / 'tools/godot/camera-checks/Program.cs').read_text())
(target / 'Mutant.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>')
PY
  if dotnet run --project "$scratch/mutant/Mutant.csproj" -- "$scratch/oracle.json" >"$scratch/mutant.log" 2>&1; then
    echo 'ERROR: six-pixel threshold mutant escaped detection.' >&2
    exit 1
  fi
  grep -q 'exactly-six-pixels-starts-pan' "$scratch/mutant.log"
  echo 'Negative control passed: the live source vectors reject the six-pixel threshold mutant.'
fi
if [[ " $* " == *" --native "* ]]; then
  python3 - "$PWD" "$scratch/native" <<'PY'
from pathlib import Path
import sys
root, target = map(Path, sys.argv[1:])
(target / 'Presentation').mkdir(parents=True)
(target / 'Scenes').mkdir()
for name in ['BattlefieldCamera.cs', 'BattlefieldGestures.cs', 'BattlefieldFocus.cs', 'BattlePresentationModels.cs', 'CameraChecks.cs']:
    (target / 'Presentation' / name).write_bytes((root / 'godot/Presentation' / name).read_bytes())
(target / 'Scenes/CameraChecks.tscn').write_bytes((root / 'godot/Scenes/CameraChecks.tscn').read_bytes())
(target / 'CameraNativeChecks.csproj').write_text('<Project Sdk="Godot.NET.Sdk/4.6.3"><PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>')
(target / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="Camera native checks"\nrun/main_scene="res://Scenes/CameraChecks.tscn"\nconfig/features=PackedStringArray("4.6", "C#", "GL Compatibility")\n[dotnet]\nproject/assembly_name="CameraNativeChecks"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n')
PY
  dotnet build "$scratch/native/CameraNativeChecks.csproj" --nologo
  "$GODOT_NET" --headless --path "$scratch/native" res://Scenes/CameraChecks.tscn
fi
