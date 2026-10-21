#!/usr/bin/env bash
set -euo pipefail
if [[ $# -gt 1 ]] || [[ $# -eq 1 && "$1" != "--packaged" ]]; then
  printf 'Usage: %s [--packaged]\n' "$0" >&2
  exit 2
fi
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$ROOT"
source tools/godot/env.sh
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-localization.XXXXXXXX")"
export XDG_DATA_HOME="$scratch/data"
timeout_seconds="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
python3 -m unittest discover -s tools/godot/localization-checks -p 'test_*.py'
python3 tools/godot/localization-checks/validate_narrative.py "$ROOT"
python3 tools/godot/localization-checks/validate_catalog.py godot/Localization/zh_CN.po --output "$scratch/catalog.json"
timeout --signal=TERM --kill-after=15s "$timeout_seconds" dotnet run --project tools/godot/localization-checks -- "$scratch/catalog.json"
python3 - "$ROOT" "$scratch" <<'PY'
from pathlib import Path
import shutil
import sys
root, scratch = map(Path, sys.argv[1:])
native = scratch / 'native'
(native / 'Localization').mkdir(parents=True)
for source in (root / 'godot/Localization').glob('*.cs'):
    shutil.copyfile(source, native / 'Localization' / source.name)
shutil.copyfile(root / 'godot/Localization/zh_CN.po', native / 'Localization/zh_CN.po')
shutil.copyfile(root / 'tools/godot/localization-checks/NativeChecks.cs', native / 'NativeChecks.cs')
shutil.copyfile(scratch / 'catalog.json', native / 'expected.json')
(native / 'LocalizationNativeChecks.csproj').write_text('''<Project Sdk="Godot.NET.Sdk/4.6.3">
<PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
</Project>\n''')
(native / 'project.godot').write_text('''config_version=5
[application]
config/name="Localization isolated checks"
run/main_scene="res://NativeChecks.tscn"
config/features=PackedStringArray("4.6", "C#", "GL Compatibility")
[dotnet]
project/assembly_name="LocalizationNativeChecks"
[internationalization]
locale/translations=PackedStringArray("res://Localization/zh_CN.po")
[rendering]
renderer/rendering_method="gl_compatibility"
''')
(native / 'NativeChecks.tscn').write_text('''[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://NativeChecks.cs" id="1"]
[node name="NativeChecks" type="Node"]
script = ExtResource("1")
''')
(native / 'export_presets.cfg').write_text('''[preset.0]
name="Linux localization checks"
platform="Linux"
runnable=true
export_filter="all_resources"
include_filter="expected.json"
exclude_filter="*.csproj,*.md"
script_export_mode=2
[preset.0.options]
binary_format/architecture="x86_64"
texture_format/s3tc_bptc=true
texture_format/etc2_astc=false
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
dotnet/embed_build_outputs=false
''')
PY
run_logged() {
  local name="$1" status=0
  shift
  timeout --signal=TERM --kill-after=15s "$timeout_seconds" "$@" > "$scratch/$name.log" 2>&1 || status=$?
  cat "$scratch/$name.log"
  if [[ "$status" != 0 ]] || grep -Eiq '(^ERROR:|leaked|segmentation fault|unhandled exception)' "$scratch/$name.log"; then
    printf 'Localization %s failed (exit %s); evidence: %s\n' "$name" "$status" "$scratch" >&2
    return 1
  fi
}
run_logged solution dotnet new sln --name LocalizationNativeChecks --output "$scratch/native"
run_logged solution_project dotnet sln "$scratch/native/LocalizationNativeChecks.sln" add "$scratch/native/LocalizationNativeChecks.csproj"
run_logged build dotnet build "$scratch/native/LocalizationNativeChecks.csproj" --nologo
run_logged import "$GODOT_NET" --headless --editor --path "$scratch/native" --import --quit
run_logged native "$GODOT_NET" --headless --path "$scratch/native"
grep -q 'PASS native localization resource:' "$scratch/native.log"
if [[ "${1:-}" == '--packaged' ]]; then
  # Export templates are installed outside the isolated check data directory.
  mkdir -p "$scratch/data/godot/export_templates" "$scratch/package"
  ln -s "$EMPIRE_TOOLCHAIN_ROOT/xdg-data/godot/export_templates/4.6.3.stable.mono" "$scratch/data/godot/export_templates/4.6.3.stable.mono"
  run_logged export "$GODOT_NET" --headless --path "$scratch/native" --export-release 'Linux localization checks' "$scratch/package/LocalizationChecks.x86_64"
  # This directory has only exported files, no project source or editor import cache.
  (cd "$scratch/package"; run_logged packaged ./LocalizationChecks.x86_64 --headless)
  grep -q 'PASS native localization resource:' "$scratch/packaged.log"
fi
printf 'Localization foundation checks passed. Evidence: %s\n' "$scratch"
