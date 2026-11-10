# Read-only assembly identity audit

Do not run the harness in this formal evidence directory. Copy only the harness and formatting configuration into a new scratch directory first. Requires .NET SDK 8 and Python 3; no external NuGet packages. The harness builds itself only and does not execute, modify, or rebuild production assemblies. Exit 0 means equality under the declared exclusions; exit 1 means mismatch.

## Reproduce in separate scratch

```bash
PACKAGE=/absolute/path/to/this/deliverable
SCRATCH=$(mktemp -d /workspace/scratch/84c9fe948755/empire-identity-rerun-XXXXXX)
cp "$PACKAGE"/{Program.cs,Audit.csproj,run.sh,selftest.py,.editorconfig} "$SCRATCH/"
cd "$SCRATCH"
export DOTNET=/workspace/scratch/84c9fe948755/empire-tools/dotnet/dotnet
export HOME=/workspace/scratch/84c9fe948755/empire-tools/home
export DOTNET_CLI_HOME="$HOME"
export XDG_DATA_HOME=/workspace/scratch/84c9fe948755/empire-tools/xdg-data
export TMPDIR="$SCRATCH/.tmp"
mkdir -p "$TMPDIR"
"$DOTNET" format whitespace Audit.csproj --include Program.cs --verify-no-changes
./run.sh /workspace/scratch/84c9fe948755/empire-gravekeeper-cast-20261110/checkpoint/final-portable/assemblies /workspace/scratch/84c9fe948755/empire-work/godot/.godot/mono/temp/bin/Debug "$SCRATCH/results"
python3 selftest.py
```

On another machine use its SDK, writable home, scratch directory and exact input copies. run.sh isolates temporary files to avoid shared .NET mutex collisions. Normal execution emits full old/new method manifests to scratch. selftest copies the new DLLs temporarily, changes one IL byte and one Field attribute byte separately, and requires both comparisons to reject with exit 1. These copies are deleted at the end.

## Exact inputs

Old directory: `checkpoint/final-portable/assemblies`. Do NOT substitute the earlier `checkpoint/assemblies`. New directory: `empire-work/godot/.godot/mono/temp/bin/Debug`.

- Old Empire.dll: `23f3ea10fdbb1bc4f0329c9c995cfe32ff1545a412126b6c90671a0575d9cd9c`
- New Empire.dll: `fd0bb3108808b43ca7540e9dc9e6c72248e1f526d049be933e7bc3a483c152e1`
- Old Empire.Core.dll: `507d7f21336e5baf00e60adfea7ae56e2b3aff34f5d1e0a2af36e7f74d4833fc`
- New Empire.Core.dll: `03a8ebc87bbbfa7ad4bf053000f52fbd1ad687f39894cfc95371d6a06097281e`

## Evidence contents

- REPORT.md: proof, scope and limitations.
- acceptance.json and run-status.json: concise gate/revalidation records.
- results/: normal summaries, per-DLL differences/normalization ranges, one gzip-compressed old manifest per DLL.
- method-manifest-hashes.json: SHA256 and lengths of all normal/negative-control method manifests. Normal old/new manifest hashes match.
- selftest-results/: summaries, per-DLL differences and process outputs; no repeated full method manifests.
- format-apply.log / format-verify.log: targeted formatting and successful verification using the included repository .editorconfig.

No DLL, bin or obj is packaged. External PDB/SourceLink and native recording provenance are separate evidence. Native provenance is documented by the independent native proof bound to the old SHA256.
