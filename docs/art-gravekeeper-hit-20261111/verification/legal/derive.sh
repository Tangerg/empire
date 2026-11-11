#!/usr/bin/env bash
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository=""
output=""
assembly_directory=""
usage() {
    echo "Usage: $0 --output EMPTY_DIRECTORY --repo-root DIRECTORY [--assembly-dir DIRECTORY]" >&2
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
[[ -n "$output" && -n "$repository" ]] || { usage; exit 2; }
repository="$(cd "$repository" && pwd)"
output="$(python3 -c 'from pathlib import Path; import sys; print(Path(sys.argv[1]).resolve())' "$output")"
case "$output" in
    "$repository"|"$repository"/*)
        echo "Output must be outside the repository; audit evidence is read-only." >&2
        exit 2
        ;;
esac
case "$output" in
    "$here"|"$here"/*)
        echo "Output must be outside the evidence package; source inputs are read-only." >&2
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

toolchain="${EMPIRE_TOOLCHAIN_ROOT:-$(dirname "$repository")/empire-tools}"
dotnet_command="$(command -v dotnet || true)"
if [[ -z "$dotnet_command" && -x "$toolchain/dotnet/dotnet" ]]; then
    dotnet_command="$toolchain/dotnet/dotnet"
    export DOTNET_ROOT="$toolchain/dotnet"
fi
[[ -n "$dotnet_command" ]] || { echo "An existing .NET 8 toolchain is required." >&2; exit 2; }
mkdir -p "$output/harness" "$output/tmp"
export TMPDIR="$output/tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
# Reuse only existing first-use metadata. All CLI writes remain in scratch.
mkdir -p "$output/home/.dotnet" "$output/xdg-data"
if [[ -d "$toolchain/home/.dotnet" ]]; then
    find "$toolchain/home/.dotnet" -maxdepth 1 -type f -name '*.sentinel' -exec cp '{}' "$output/home/.dotnet/" \;
    find "$toolchain/home/.dotnet" -maxdepth 1 -type f -name '*FirstUseSentinel' -exec cp '{}' "$output/home/.dotnet/" \;
fi
if [[ -d "$toolchain/xdg-data/NuGet" ]]; then
    cp -R "$toolchain/xdg-data/NuGet" "$output/xdg-data/"
fi
export DOTNET_CLI_HOME="$output/home" HOME="$output/home"
export XDG_DATA_HOME="$output/xdg-data" XDG_CONFIG_HOME="$output/xdg-config"
export XDG_CACHE_HOME="$output/xdg-cache" XDG_RUNTIME_DIR="$output/runtime"
export NUGET_PACKAGES="$output/nuget"
cp "$here/harness/Probe.csproj" "$here/harness/Program.cs" "$here/harness/.editorconfig" "$output/harness/"
python3 "$here/extract-translation-lookup.py" \
    --source "$repository/godot/Localization/zh_CN.po" \
    --output "$output/translation-lookup.json"

# Build only the copied scratch harness against existing game assemblies.
"$dotnet_command" build "$output/harness/Probe.csproj" \
    -p:EmpireAssemblyDirectory="$assembly_directory" -p:UseSharedCompilation=false | tee "$output/build.txt"
"$dotnet_command" "$output/harness/bin/Debug/net8.0/Probe.dll" \
    "$repository" "$here" "$output" | tee "$output/derivation.txt"
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
