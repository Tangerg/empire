# Reject zero-exit native script errors

Baseline: `01555de29bd1a11d6bfe97ba31417f2fe573d94b`.

## Reproduction and boundary

An isolated script run with official Godot 4.6.3 .NET emitted `SCRIPT ERROR:`
after an invalid property access, then exited 0. The unchanged
`tools/godot/native-process.sh` returned 0 as well. Its diagnostic predicate
recognized `ERROR:` but not Godot's separate script-error prefix.

This is a demonstrated acceptance-gate defect, not evidence that the shipped
C# game produced this script error, and not a diagnosis of earlier native
shutdown failures. No game code, combat rule, HUD forecast, art or save changes.

The common editor/scene wrapper now rejects a line beginning `SCRIPT ERROR:`.
It still retains stdout/stderr unchanged, reports the child's actual exit code,
and rejects nonzero exits, timeouts, leak and crash diagnostics. A normal warning
that merely mentions the prefix within its message is not a script-error line.

## Verification

- The new tests ran first against the unchanged wrapper: 13 tests, four expected
  failures (editor import, scene after a success marker, stdout and stderr).
- With the one-line predicate change, the same 13 tests pass. The aggregate
  fixtures explicitly simulate processes; they are not native-game scene runs.
- The same actual native script then returned wrapper exit 1 while the child
  still exited 0. The original error and backtrace remain in the retained log.
- Shell syntax and `git diff --check` pass.
- The first complete `verify-native.sh` attempt failed at
  `PaintedSurfaceBroadPhaseChecks`: child exit 139, wrapper/aggregate exit 1.
  Its native log contained only the engine banner, no completed assertion
  marker. Later scenes were not run by that attempt. The failure is retained in
  `full-native-first-failed.txt`, not counted as a pass.
- A single bounded rerun of the same scene, unchanged DLLs and command passed
  28,251 assertions with normal exit and the strict diagnostic gate. This does
  not establish the crash's cause or a fix. A new complete gate is pending.
- Before that failure, all 17 core groups and 16 independent level save/replays
  passed. All 3,425 ordered action/event/receipt traces exactly match the prior
  published full-gate log. Source guards, builds, import and the preceding six
  native scenes passed, including 18,032 assembled-ground assertions.

The focused recheck used these unchanged Debug assembly SHA-256 values:

- `Empire.dll`: `aab4afa82b2ba29910d6e601044aa16f07c739e71cc1a8e4a3426bdcb7f40c47`
- `Empire.Core.dll`: `6d7acdaa7b70bddf48b3cbb47f9224898628470c24531b5e1ec7f90427a09ccf`

Evidence is in [the verification directory](verification/native-script-diagnostics-20261110/).
`probe.gd.txt` is a non-imported copy of the diagnostic script, not game content.
To repeat it, copy it to a temporary `.gd` file and run the configured official
Godot executable with `--headless --script /absolute/temporary/probe.gd` through
`native-process.sh`. Expected child exit: 0; expected wrapper exit: 1.

The restored workspace toolchain is Microsoft .NET SDK 8.0.427 and Godot 4.6.3
.NET. Downloads were verified against Microsoft's release SHA-512 and the
Godot release asset's SHA-256. This installation is workspace-local.

No graphical or exported-product acceptance is claimed by this work.
