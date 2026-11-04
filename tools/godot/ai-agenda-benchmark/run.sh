#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../../.." && pwd)"
source "$root/tools/godot/env.sh"
[[ $# == 3 && "$1" == /* && -f "$1" ]] || { echo 'Usage: run.sh /absolute/Empire.Core.dll label round' >&2; exit 2; }
work="$(mktemp -d "${TMPDIR:-/tmp}/empire-ai-agenda-benchmark.XXXXXX")"
cp "$root"/tools/godot/ai-agenda-benchmark/{Program.cs,AiAgendaBenchmark.csproj,NuGet.Config} "$work/"
dotnet build "$work/AiAgendaBenchmark.csproj" -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false "-p:EmpireCoreAssembly=$1" > "$work/build.log" 2>&1 || { cat "$work/build.log" >&2; exit 1; }
printf 'Benchmark artifacts: %s\n' "$work" >&2
DOTNET_TieredCompilation=0 timeout --signal=TERM --kill-after=15s 300 dotnet "$work/bin/Release/net8.0/AiAgendaBenchmark.dll" "$root" "$2" "$3"
