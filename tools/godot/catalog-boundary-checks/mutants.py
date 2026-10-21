"""Compile an isolated raw-definition leak and require the public consumer to reject it."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(sys.argv[1]).resolve()
scratch = Path(tempfile.mkdtemp(prefix="empire-catalog-mutant."))
core = scratch / "godot/Empire.Core"
shutil.copytree(root / "godot/Empire.Core", core, ignore=shutil.ignore_patterns("bin", "obj"))
checks = scratch / "tools/godot/catalog-boundary-checks"
shutil.copytree(root / "tools/godot/catalog-boundary-checks", checks,
                ignore=shutil.ignore_patterns("bin", "obj", "__pycache__"))
source = core / "State/StateContent.cs"
text = source.read_text()
needle = 'definitions[family].TryGetValue(id, out var entry) ? (JsonObject)entry.DeepClone() : null;'
if text.count(needle) != 1:
    raise RuntimeError("Catalog clone mutation target changed; update the exact semantic guard.")
source.write_text(text.replace(needle, 'definitions[family].TryGetValue(id, out var entry) ? entry : null;'))
project = checks / "CatalogBoundaryChecks.csproj"
build = subprocess.run(["dotnet", "build", str(project), "--disable-build-servers", "--nologo"],
                       capture_output=True, text=True)
(scratch / "build.log").write_text(build.stdout + build.stderr)
if build.returncode:
    raise RuntimeError(f"Mutant must compile, not fail the build. See {scratch}/build.log")
run = subprocess.run(["dotnet", str(checks / "bin/Debug/net8.0/CatalogBoundaryChecks.dll"), str(root)],
                     capture_output=True, text=True)
output = run.stdout + run.stderr
(scratch / "run.log").write_text(output)
if run.returncode == 0 or "later forecasts retain canonical weapon power" not in output:
    raise RuntimeError(f"Mutant did not fail the expected semantic assertion. See {scratch}/run.log")
print(f"PASS raw catalog leak mutant compiled and was rejected by public forecasts: {scratch}")
