# Independent native evidence verifier

`verify-native-evidence.py` is read-only. It does not launch Godot, build/import
assets, alter saves, record captures or encode videos. Pillow is used for pixel
comparison; FFmpeg/FFprobe decode and inspect an existing video.

Archive verification from the repository root, repeated for `idle-left` and
`recovery-left`:

```sh
base=docs/art-gravekeeper-idle-20261110/verification/native
run="$base/idle-right"
python "$base/verify-native-evidence.py" --kind idle-right \
  --proof "$run/native-proof.json" --traces "$run/native-frames.jsonl" \
  --video "$run/native-lossless.mp4" \
  --input-save "$run/native-input.json" --output-save "$run/native-output.json" \
  --source "$run/native-source.txt" --hashes "$run/native-assemblies.sha256"
python "$base/test-native-evidence.py"
```

Raw mode replaces `--proof`/`--traces` with `--capture-dir <actual capture>`.
`--video` is optional in raw mode; when absent, no video claim is made. Native
exit/log files default to siblings of the source record and can be specified
with `--native-exit` and `--session-log`. `--legal-root` defaults to the sibling
portable legal archive. `--output` creates a new JSON report and refuses to
replace an existing report. Missing evidence is an error, not a pass.

## Checks

- Exact tested source, Godot/tools tree and three frozen DLL hash records; native
  process exit0, matching completed-capture count/stride, no error/leak lines.
- Actual actor 4 artwork, frame anchors, signed uniform scale, root/support and
  facing. Idle captures contain exactly 103 observations at stride 3 and fixed 60 Hz,
  span5.1 simulation seconds from first to last, remain nonpresenting, show all
  four frames and at least two wraps. Each full pose sequence must fit the exact
  700/500/500/700ms cycle with one compatible initial phase; initial frame 0 is
  not assumed. Every scene actor's idle root/support and membership stays fixed.
- Recovery count is read from the actual trace. All six cast poses, idle recovery
  and first nonpresenting endpoint must fit 600/800 ms at fixed 60 Hz. Only an
  internal half-open pose boundary within 1e-7 ms may select either adjacent frame,
  accounting for IEEE-double accumulation at 500 ms. Whole-frame shifts and the
  600 ms source/800 ms receipt endpoints are not relaxed.
- Native input equals the exact preserved legal input. Entire native output
  equals the legal expected result, omitting only campaign.savedAt and
  battle.savedAt; both must change from the loaded input. The recovery target 3
  must change 39→69 HP and keeper 4 must finish west-facing.
- All original trace-line hashes, decoded full-frame RGB hashes, video frame
  count/fps, selected native PNG hashes/RGB, and every diagnostic nearest-neighbor
  crop tile. Archive source evidence, additional still and diagnostic-sheet
  hashes are also checked. RGB videos do not preserve original PNG alpha.

The 23 in-memory controls include positive idle/recovery/save witnesses and
rejection of trace cadence/count, pose, facing, scale, contact, registration,
presentation, recovery/receipt endpoint, RGB, source/DLL and full-save mutations.
They are synthetic verification tests, not additional native gameplay evidence.

The trace has no effect-lifetime or opacity field. Actor recovery and overall
presentation completion corroborate the 600/800 ms contract at one-frame sampling
resolution; a new recipient-effect trace is not invented. Capture wall-clock
elapsedMs is not simulation time. The verifier makes no realtime-performance,
export, exhaustive occlusion or full-campaign claim.
