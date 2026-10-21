# Empire Linux x86_64 package

## Run

Extract the entire archive into one directory, then run `./Empire.x86_64`.
Keep `Empire.pck` and `data_Empire_linuxbsd_x86_64/` next to the executable.
Keep THIRD-PARTY-NOTICES/ with the package; it preserves vendor notices, not a
legal audit or license for Empire itself.
The directory includes the .NET runtime. Running the package does not require
the editor, .NET SDK, Node, TypeScript, or the source checkout.

This build targets Linux x86_64. A graphical desktop and OpenGL compatibility
support are required for normal play. Headless checks are not visual acceptance.
This package is not for macOS or Windows. A separate macOS cross-export is
explicitly untested; see MACOS-PACKAGE.md in the source checkout.

Saves live outside this package under the normal Godot user-data directory,
usually `~/.local/share/godot/app_userdata/Empire GrayFlag Migration/`.
`XDG_DATA_HOME` overrides its base directory. Preserve both
`grayflag-campaign-autosave.json` and `grayflag-campaign-save.json` when moving
saves. Replacing the package directory does not require clearing either save.

## Build from source

Use the official Godot 4.6.3 .NET editor, Microsoft .NET SDK 8, and matching
official 4.6.3 .NET Linux x86_64 export templates. Set
`EMPIRE_TOOLCHAIN_ROOT` to the local toolchain root used by
`tools/godot/env.sh`; the default is a sibling `empire-tools` directory.
Then run from the repository root:

```sh
bash tools/godot/export-linux.sh /absolute/new-output-directory
```

The directory must be new. The script builds C#, imports resources, exports the
recorded `Linux x86_64` release preset, smoke-tests the resulting executable,
and creates `Empire-linux-x86_64.tar.gz` plus `SHA256SUMS`. Logs remain beside
the package. The script does not install tools, publish files, clear existing
builds, or touch ordinary saves. First export may restore official .NET runtime
packages through NuGet. `Empire.sln` is required by Godot's .NET export plugin.
Build, import and export have 20-, 15- and 30-minute limits, respectively,
followed by a 30-second termination grace. Each smoke process has a three-minute
limit and a 15-second grace. A timeout is a failed check, not a success.

The production preset includes runtime JSON and imported art, but no source-test
scenes, oracle fixtures, source text or development documentation. ExportRelease
also excludes the source-test classes from compilation. Existing development
tests are unchanged and still run in Debug. One dedicated `ReleaseDiagnostics`
scene checks the actual shipped application using only real runtime content,
authored story and art; it refuses any package containing a `tests` directory.
Godot's official release templates reject scene-path overrides. An explicit,
whitelisted `-- --package-check=release` argument selects this diagnostic through
the application instead; normal launch is
unchanged. All diagnostic modes refuse non-fresh, non-temporary or symlinked
user-data namespaces before opening game state; normal saves are never loaded.

## Repeat the packaged checks

```sh
bash tools/godot/smoke-linux-package.sh /absolute/package/Empire.x86_64
```

Each process starts outside the checkout with a fresh `empire-package-smoke.*`
user-data namespace under `${TMPDIR:-/tmp}`, an empty executable search path,
and no inherited .NET SDK settings. Set `TMPDIR` to an existing, roomy temporary
directory if `/tmp` is constrained; its absolute path is passed into each process.
The checks cover title startup, imported JSON/art, authored story and choice,
the first battle and a legal move, actual manual save/load/restart callbacks,
return to camp and campaign continuation, and queued resource teardown. They
require normal exit and success markers
and reject errors, crashes and leak diagnostics after shutdown. Temporary test
data and logs are retained for inspection; no existing user data is deleted.
Unknown and duplicate diagnostic arguments must exit with code 2. A generated
pre-existing-save sentinel must also be refused and remain unchanged.

This smoke does not finish a battle or campaign, test every order family, or
establish every campaign branch or victory route. It does not establish graphical
appearance, desktop input usability, other Linux distributions, macOS, or Windows.
