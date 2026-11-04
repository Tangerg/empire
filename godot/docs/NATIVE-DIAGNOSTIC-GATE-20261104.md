# Native process completion and import diagnostics

Baseline: `9ee81f94171f0078b926cee9e870c76e5eeb5988`.

## Evidence and scope

The native editor import in the preceding morale gate returned exit 0 but emitted:

```
ERROR: EditorSettings not instantiated yet when getting setting "export/android/android_sdk_path".
```

That original run and a separate clean recheck are described in `MORALE-FEEDBACK-20261104.md`; the same intermittent engine diagnostic was already recorded in `MENU-SAVE-ADMISSION-20261104.md`. No claim is made that the Godot engine error itself has been repaired.

The repository's aggregate `verify-native.sh` checked import process exit alone, while its native scene runner additionally rejected error/leak/teardown diagnostics. Consequently an import could visibly report an error and still reach the aggregate success line. A new isolated test executed the **original aggregate script** against simulated tools: an editor process emitted that exact error on stderr and returned 0; all later scenes ran and the final success line was printed. The regression failed as expected. This control-flow reproduction is separate from the retained real Godot diagnostic, not a claim that simulated tool processes exercised gameplay.

A parallel read-only review found no new substantiated formal battle playback defect. In particular, all 16 formal levels have fog disabled and no formal UI/scenario path was found to enable it. Hypothetical hidden-event behavior was not used as a reason to change the current campaign. This batch changes no game code, content, maps, art, rules, save format or save admission.

## One completion boundary

`tools/godot/native-process.sh` now owns the common timeout, combined stdout/stderr log, process exit and existing diagnostic predicate for both editor import and project native scenes. It preserves the full output and reports the label, underlying exit status and retained log path on failure. A PASS line printed before teardown cannot override a later diagnostic or nonzero exit. It does not retry, filter out errors, discard logs or treat known engine errors as warnings.

The aggregate still owns command order, scene composition, and each scene's fresh `XDG_DATA_HOME`. The editor retains its existing toolchain data namespace and gets a unique retained import log. The same scene list, oracle arguments, signal/grace timeout behavior and existing warning policy are preserved. No new dependency or runtime setting is required.

## Regression and verification

- `native-process-tests.py`: **10 tests**, including eight diagnostic/stream combinations. Covers the exact zero-exit editor error, first-scene failure after a PASS line, clean aggregate traversal through all 29 native scenes, preserved oracle arguments and 29 distinct save namespaces. Direct process checks cover stdout/stderr, warning retention, quoting/spaces/Unicode arguments, nonzero exit, timeout, missing command and invalid invocation.
- Three deliberate broken variants are rejected by their intended regressions: remove diagnostic detection, ignore process exit status, or bypass the import boundary in the aggregate. All expected failing test invocations exit 1; these negative runs are not represented as successful native processes.
- The original red run, full logs and later verification are retained separately. Code checkpoint: `9fd00efa10cabf3e49631bb7ceb3dd4d0559bcd6`.
- **The actual repository aggregate script** ran uninterrupted from **2026-11-04 13:08:04 to 13:53:32 UTC (45m28s)** and exited 0. Its timed command trace confirms **43 checks**, including the real editor import and **29 project native scenes**. The real processes passed both exit-code and diagnostic checks; no external substitute driver or skipped phase supplied this success.
- Core contracts: **13 passed**, all **16 formal independent levels**, **3,425 legal orders**, and strict save/replay round trips. Per-level counts and state digests match the preceding baseline. Independent-level wins/losses are not a claim of a carried-state campaign victory.
- Debug builds had zero warnings/errors. The source-selection, formatting, UID metadata and authored-map negative controls passed. Relevant native results include animation 844; HUD 510; dossier focus 73; scenario announcements 337; morale 74; arrivals 96; battle-result flow 371; campaign navigation **8,854 assertions, zero failed groups**; campaign UI 205; lighting lifetime 106; lighting 1,791; scenery 8,753.
- The complete aggregate and all 30 native-process logs were rechecked without error/leak/crash diagnostics. Existing warning texts and counts match the preceding gate after normalizing temporary paths; deliberate bad-save and missing-art fixtures, duplicate mouse notifications, ignored provenance project and authored `runtimeReady=false` diagnostics were not hidden.
- Gate source hashes stayed unchanged and the checkpoint worktree remained clean. The fresh build's binaries differ from the previous commit's pre-build hashes: generated assembly informational versions record this new checkpoint. The new tested binary hashes were saved separately; equality with the previous build is not claimed. No runtime source changed.
- This round did not repeat the separate localization foundation or ExportRelease build. Those passed in the preceding arrival batch; neither their inputs nor runtime code changed here. The final commit only records these results.

The process boundary applies to [Godot's command-line editor/import and headless execution](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html). Headless checks are not GUI, physical-device or exported-package visual acceptance.

```sh
python3 tools/godot/native-process-tests.py
EMPIRE_CHECK_TIMEOUT_SECONDS=900 bash tools/godot/verify-native.sh
```
