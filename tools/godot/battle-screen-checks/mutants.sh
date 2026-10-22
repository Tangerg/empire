#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
source "$root/tools/godot/env.sh"
export EMPIRE_CHECK_TIMEOUT_SECONDS="${EMPIRE_CHECK_TIMEOUT_SECONDS:-300}"
python3 - "$root" <<'PY'
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tempfile

root = Path(sys.argv[1])
source = root / "godot"
timeout = int(os.environ["EMPIRE_CHECK_TIMEOUT_SECONDS"])
if timeout <= 0:
    raise SystemExit("EMPIRE_CHECK_TIMEOUT_SECONDS must be positive.")
dotnet = Path(os.environ["DOTNET_ROOT"]) / "dotnet"
godot = Path(os.environ["GODOT_NET"])
for path in [dotnet, godot, source / ".godot/imported"]:
    if not path.exists():
        raise SystemExit(f"Missing native tool/import dependency: {path}; build and import the Godot project first.")

# No campaign host, save fixtures, unrelated scenes, binaries or caches are copied.
# Existing imported textures and runtime documents are read-only input to the scene.
excluded = {
    "BattleResultScreen.cs", "CampaignMenu.cs", "CampaignMenuScreen.cs", "CampaignShell.cs",
    "CampaignStagingScreen.cs", "StoryScreen.cs", "NativeCapture.cs", "TerrainPreview.cs",
}
checks = {"BattleScreenChecks.cs", "TacticalSceneInputChecks.cs", "BattleTextChecks.cs"}
mutants = {
    "canonical-fog-replaced-by-storage": (
        "Presentation/BattleScreen.cs",
        [("_overlay.ShowPublicState(_tactical!.InteractiveCells,", "_overlay.ShowPublicState(_state[\"map\"]![\"tiles\"]!.AsArray().Select((tile, index) => new BoardCell(index % _state[\"map\"]![\"width\"]!.GetValue<int>(), index / _state[\"map\"]![\"width\"]!.GetValue<int>())).ToHashSet(),")],
        "Rendered fog must consume the exact canonical interactive-cell projection"),
    "hidden-port": (
        "Presentation/BattleScreen.cs",
        [("public void BindSession(GameText text, BattleText battleText, ITacticalBattleSession session,", "public void BindSession(GameText text, BattleText battleText, object candidate,"),
         ("        ArgumentNullException.ThrowIfNull(session);", "        var session = (ITacticalBattleSession)candidate;\n        ArgumentNullException.ThrowIfNull(session);")],
        "BattleScreen must declare its complete consumer port"),
    "stale-live-revision": (
        "Presentation/BattleScreen.Tactical.cs",
        [("_session.Revision == stamp.Revision && ", "")],
        "Still-mounted HUD controls cannot use stale choices"),
    "closed-reaction-hud": (
        "Application/GrayFlagSession.Hud.cs",
        [("engine.Combat.Forecasts.Reactions.All().Select(stance", "engine.Combat.Forecasts.Reactions.All().Where(stance => stance.Id != \"plugin-dodge\").Select(stance")],
        "Mounted reaction controls must retain resolved registry order"),
}

with tempfile.TemporaryDirectory(prefix="empire-screen-mutants-") as directory:
    project = Path(directory) / "godot"

    def copy(path):
        destination = project / path.relative_to(source)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)

    for path in (source / "Empire.Core").rglob("*"):
        if path.suffix not in {".cs", ".csproj"} or any(part in {"bin", "obj"} for part in path.parts):
            continue
        copy(path)
    for path in (source / "Application").glob("GrayFlagSession*.cs"):
        copy(path)
    for path in (source / "Presentation").rglob("*.cs"):
        if path.name in excluded or path.name.endswith("Checks.cs") and path.name not in checks:
            continue
        copy(path)
    for path in (source / "Localization").iterdir():
        if path.is_file():
            copy(path)
    copy(source / "Scenes/BattleScreenChecks.tscn")
    for name in ["assets", "data"]:
        (project / name).symlink_to(source / name, target_is_directory=True)
    (project / ".godot").mkdir()
    (project / ".godot/imported").symlink_to(source / ".godot/imported", target_is_directory=True)
    (project / "Empire.csproj").write_text('''<Project Sdk="Godot.NET.Sdk/4.6.3">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><Compile Remove="Empire.Core/**/*.cs"/><ProjectReference Include="Empire.Core/Empire.Core.csproj"/></ItemGroup>
</Project>
''')
    (project / "project.godot").write_text('''config_version=5
[application]
run/main_scene="res://Scenes/BattleScreenChecks.tscn"
config/name="Empire session port checks"
config/features=PackedStringArray("4.6", "C#", "GL Compatibility")
[display]
window/size/viewport_width=1280
window/size/viewport_height=720
[rendering]
renderer/rendering_method="gl_compatibility"
[dotnet]
project/assembly_name="Empire"
''')

    def run(arguments, environment=None):
        return subprocess.run(["timeout", "--signal=TERM", "--kill-after=15s", str(timeout), *map(str, arguments)],
                              cwd=project, env=environment, capture_output=True, text=True, timeout=timeout + 20)

    for name, mutation in [("baseline", None), *mutants.items()]:
        if mutation:
            filename, replacements, expected = mutation
            target = project / filename
            original = target.read_text()
            modified = original
            for old, new in replacements:
                if modified.count(old) != 1:
                    raise SystemExit(f"Mutation must match exactly once: {name}: {old}")
                modified = modified.replace(old, new)
            target.write_text(modified)
        try:
            build = run([dotnet, "build", project / "Empire.csproj", "--nologo", "--disable-build-servers", "-m:1", "-p:UseSharedCompilation=false"])
            if build.returncode:
                raise SystemExit(f"Compilation failure is not a killed mutant: {name}\n" + build.stdout + build.stderr)
            environment = os.environ.copy()
            data = Path(directory) / (name + "-data")
            data.mkdir()
            environment["XDG_DATA_HOME"] = str(data)
            result = run([godot, "--headless", "--path", project, "res://Scenes/BattleScreenChecks.tscn"], environment)
            output = result.stdout + result.stderr
            if mutation:
                if result.returncode in {0, 124, 137} or expected not in output:
                    raise SystemExit(f"Survived, timed out, or failed for an unrelated reason: {name}\n" + output)
                print(f"KILLED {name}: {expected}", flush=True)
            elif result.returncode or any(marker in output.lower() for marker in ["error:", "leaked", "unhandled exception", "segmentation fault"]):
                raise SystemExit("Native baseline failed\n" + output)
            else:
                print("PASS isolated native BattleScreen baseline", flush=True)
        finally:
            if mutation:
                target.write_text(original)
PY
