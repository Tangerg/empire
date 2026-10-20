#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
assembly="${EMPIRE_CORE_ASSEMBLY:-$root/godot/Empire.Core/bin/Debug/net8.0/Empire.Core.dll}"
if [[ ! -f "$assembly" ]]; then
  printf 'Build Empire.Core first: dotnet build %s/godot/Empire.Core/Empire.Core.csproj\n' "$root" >&2
  exit 1
fi
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-determinism.XXXXXXXX")"
trap 'rm -rf "$scratch"' EXIT
cp "$source_dir/Program.cs" "$source_dir/Verify.csproj" "$source_dir/NuGet.Config" "$scratch/"
node "$source_dir/generate-vectors.cjs" "$scratch"
dotnet build "$scratch/Verify.csproj" --configfile "$scratch/NuGet.Config" \
  --disable-build-servers --nologo "-p:EmpireCoreAssembly=$assembly"
dotnet "$scratch/bin/Debug/net8.0/Verify.dll" "$scratch"
