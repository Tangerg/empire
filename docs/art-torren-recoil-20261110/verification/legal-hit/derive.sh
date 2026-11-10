#!/usr/bin/env bash
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository="$(cd "$here/../../../.." && pwd)"
output=""
assembly_directory=""
usage() {
    echo "Usage: $0 --output EMPTY_DIRECTORY [--repo-root DIRECTORY] [--assembly-dir DIRECTORY]" >&2
}
while [[ $# -gt 0 ]]; do
    case "$1" in
        --output|--repo-root|--assembly-dir)
            [[ $# -ge 2 ]] || { usage; exit 2; }
            case "$1" in
                --output) output="$2" ;;
                --repo-root) repository="$2" ;;
                --assembly-dir) assembly_directory="$2" ;;
            esac
            shift 2
            ;;
        *) usage; exit 2 ;;
    esac
done
[[ -n "$output" ]] || { usage; exit 2; }
repository="$(cd "$repository" && pwd)"
output="$(python3 -c 'from pathlib import Path; import sys; print(Path(sys.argv[1]).resolve())' "$output")"
case "$output" in
    "$repository"|"$repository"/*)
        echo "Output must be outside the repository; audit evidence is read-only." >&2
        exit 2
        ;;
esac
if [[ -e "$output" ]] && { [[ ! -d "$output" ]] || [[ -n "$(ls -A "$output")" ]]; }; then
    echo "Output must be a new or empty directory; refusing to overwrite a previous run." >&2
    exit 2
fi
assembly_directory="${assembly_directory:-$repository/godot/.godot/mono/temp/bin/Debug}"
assembly_directory="$(cd "$assembly_directory" && pwd)"
for name in Empire.dll Empire.Core.dll GodotSharp.dll; do
    [[ -r "$assembly_directory/$name" ]] || { echo "Missing existing assembly: $name" >&2; exit 2; }
done

if ! command -v dotnet >/dev/null 2>&1; then
    source "$repository/tools/godot/env.sh"
fi
command -v dotnet >/dev/null 2>&1 || { echo "An existing .NET 8 toolchain is required." >&2; exit 2; }
mkdir -p "$output/harness" "$output/tmp"
export TMPDIR="$output/tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cp "$here/harness/Probe.csproj" "$here/harness/Program.cs" "$output/harness/"
python3 "$here/extract-translation-lookup.py" \
    --source "$repository/godot/Localization/zh_CN.po" \
    --output "$output/translation-lookup.json"

# Build only the copied scratch harness against existing game assemblies.
dotnet build "$output/harness/Probe.csproj" \
    -p:EmpireAssemblyDirectory="$assembly_directory" | tee "$output/build.log"
dotnet "$output/harness/bin/Debug/net8.0/Probe.dll" \
    "$repository" "$here" "$output" | tee "$output/derivation.log"
python3 - "$assembly_directory" "$output" <<'PY'
import hashlib
from pathlib import Path
import sys
assemblies, output = map(Path, sys.argv[1:])
with (output / "runtime-assemblies-sha256.txt").open("w") as report:
    for name in ("Empire.dll", "Empire.Core.dll", "GodotSharp.dll"):
        report.write(f"{hashlib.sha256((assemblies / name).read_bytes()).hexdigest()}  {name}\n")
PY
printf 'Verified result directory: %s\n' "$output"
