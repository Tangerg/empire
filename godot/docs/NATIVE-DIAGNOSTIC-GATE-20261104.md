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
- The original red run, full logs and later verification are retained separately. Final real-toolchain results will be recorded after the implementation checkpoint.

The process boundary applies to [Godot's command-line editor/import and headless execution](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html). Headless checks are not GUI, physical-device or exported-package visual acceptance.

```sh
python3 tools/godot/native-process-tests.py
EMPIRE_CHECK_TIMEOUT_SECONDS=900 bash tools/godot/verify-native.sh
```
