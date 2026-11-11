# Actual native gravekeeper hit-right

This archive contains 50 actual 960×640 native framebuffer observations at stride 1, from a completed motion capture. Every observation is encoded once at 60 fps. The first-to-last span is 0.816667 simulation seconds. The container duration is 0.833333 s, including the last frame display interval. Readback affects pacing. Neither elapsed trace timestamps nor this movie claim wall-clock performance.

The legal witness contains one AI archer 18 movement step, a 33-damage nonfatal hit on keeper 4 (37→4 HP), and the normal next AI endTurn. The complete F5 output equals that independently engine-produced settled output except for exactly campaign.savedAt and battle.savedAt. Both timestamps changed. Source contract durations are 110 ms per AI movement cell, 320 ms source release, 600 ms source recovery, 320 ms recoil and 420 ms impact. The first captured frame can occur after movement starts. Recorded samples are checked against recoil/impact boundaries at 60 Hz; they do not measure exact sub-frame timing or effect opacity.

## Evidence and inspection

- native-lossless.mp4 is lossless RGB H.264. Every decoded RGB frame was compared byte-for-byte with the original native PNG RGB buffer. PNG alpha is not preserved by RGB video. Original raw PNG hashes remain in native-proof.json; the original complete PNG sequence remains outside Git.
- native-frames.jsonl preserves every original native JSON object byte-for-byte, in order, with one added LF separator per object. Each original trace hash and the complete JSONL hash are verified. It records actor art/frame, facing, root/support, observation counters and presenting, but not effect opacity or event dispatch.
- native-observation-*.png are untouched full-frame source PNGs. native-startup.png is an untouched separate TITLE SCREEN capture, not a settled battlefield screenshot.
- native-keeper-*-3x.png sheets contain only exact nearest-neighbor 3× crops plus labels outside each tile. Full-frame crop box: [393, 208, 454, 282]. They are diagnostic inspection aids, not altered game views.
- native.log, native.exit, input/output saves, native-source.txt and assembly manifests are copied byte-for-byte. Source logs can retain original engineering workspace paths for provenance. Reproduction scripts do not depend on those paths.
- native-proof.json records all file hashes, original PNG/RGB/trace hashes, still indices, crop geometry, observed transitions and source identity. frozen-identity.json is the separately frozen expected identity. SHA-256 manifests detect content changes relative to that trusted anchor; they do not establish authenticity against wholesale replacement of both evidence and its trusted anchor.

Keeper transitions by zero-based observation: 000: gravekeeper-breathe-v1 / 0, 024: gravekeeper-recoil-v1 / 0, 027: gravekeeper-recoil-v1 / 1, 033: gravekeeper-recoil-v1 / 2, 039: gravekeeper-recoil-v1 / 3, 043: gravekeeper-breathe-v1 / 0

## Completed UI route

Normal launch; automatic title-screen screenshot; F8 once; F9 once; observe the AI attack; observe player 1 turn 11; F5 once; screenshot shows 已保存第11回合的进度; click the window-manager close button; verify window gone, terminal prompt and native.exit 0; compare the complete saved output. No battle input, zoom, pan, or state/clock/camera modification.

This packaging step does not establish visual acceptance. A reviewer must inspect the actual video/stills and live UI before claiming clarity, visibility or artistic acceptance.

## Reproduce and verify

From the directory containing the three Python scripts, use Python 3, Pillow, ffmpeg (libx264rgb), and ffprobe:

```sh
python package-native.py --repo "$REPO" --run-dir "$RUN" --capture-dir "$CAPTURE"   --assemblies "$ASSEMBLIES" --identity frozen-identity.json --legal-root "$LEGAL"   --kind hit-right --crop 393 208 454 282 --route "$CONFIRMED_ROUTE" --output "$NEW_EMPTY_OUTPUT"
python verify-native-evidence.py --kind hit-right --identity frozen-identity.json   --proof "$ARCHIVE/native-proof.json" --traces "$ARCHIVE/native-frames.jsonl"   --video "$ARCHIVE/native-lossless.mp4" --input-save "$ARCHIVE/native-input.json"   --output-save "$ARCHIVE/native-output.json" --legal-root "$LEGAL"
python test-native-evidence.py
```

Only completed observations are packaged. No game is launched, imported or built, and no save/game state is edited. Use an empty output directory. All script inputs are explicit parameters or relative paths.
