# Actual native gravekeeper idle-right

This is a right-facing, unmirrored native UI recording from the frozen tested build. All four registered breathing poses appear with the keeper root and support plane fixed. The sequence covers more than two 2.4-second cycles and two rest-pose wraps. It begins at the current idle phase rather than resetting the animation for capture.

## Capture and timing

- Source capture: `idle-20261110-224943-001`.
- 103 actual 960 × 640 framebuffer observations; capture stride 3.
- Engine configured at 60 simulation frames per second. The original observations are encoded at 20 frames per second, without duplicated, interpolated or synthesized frames.
- First-to-last observation span: 5.1 simulation seconds. Video container duration: 5.15 seconds, including the final frame's display interval.
- Readback affects frame pacing. Neither elapsed trace timestamps nor the movie claim wall-clock or real-time performance.

`native-lossless.mp4` uses lossless RGB H.264. Every decoded full-frame RGB buffer was compared byte-for-byte with its source PNG. The video does not preserve the PNG alpha channel. `native-proof.json` records the SHA-256 of each original PNG, decoded RGB buffer and trace, plus the movie and provenance files. The original raw PNG sequence remains outside Git.

`native-frames.jsonl` retains every original per-frame JSON object in order, with a newline separator added. It records actors, facing, positions, support planes, art/frame selection, capture/render observation counters, elapsed observations and `presenting`. It does not record opacity or recipient effect events.

## Inspection files

`native-observation-*.png` are untouched full-frame native screenshots. `native-startup.png` is the separate untouched automatic startup title-screen capture. Any settled battlefield checks before recording were live UI observations and are not archived screenshots. The labeled `native-keeper-*-3x.png` sheets are diagnostic crops from the actual observations, enlarged only by nearest-neighbor sampling. Their crop box is `[448, 248, 505, 316]` in full-frame screen coordinates. These contact sheets are inspection aids, not new artwork or composited game views. The proof records every tile's source observation and crop geometry.

Keeper pose starts by zero-based observation:

000: gravekeeper-breathe-v1 / 1, 005: gravekeeper-breathe-v1 / 2, 015: gravekeeper-breathe-v1 / 3, 029: gravekeeper-breathe-v1 / 0, 043: gravekeeper-breathe-v1 / 1, 053: gravekeeper-breathe-v1 / 2, 063: gravekeeper-breathe-v1 / 3, 077: gravekeeper-breathe-v1 / 0, 091: gravekeeper-breathe-v1 / 1, 101: gravekeeper-breathe-v1 / 2

## Native route and save provenance

Normal UI route: F9, Space, Space, Escape, live settled UI inspection, F8, wait for all 103 observations, F5, verify the saved banner, close the window.

Keyboard selection centers the camera through the game's normal behavior. No saved state, facing, camera, receipt, clock or frame was patched. The scene is the official isolated campaign save. `native-input.json`, `native-output.json`, `native-source.txt`, `native-assemblies.sha256`, `native-assemblies-sha256.txt`, `native.log`, `native.exit` and `full-save-comparison.json` preserve the completed run and its result. The complete official F5 output equals the legal input after removing only `campaign.savedAt` and `battle.savedAt`. Both writer timestamps changed.

Source commit: `869078976dd21f5d82932dc4151bdd03fa973c1a`. Godot tree: `cfe370846c78c10100fbcfc8d19965efd96be697`. Tools tree: `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`. The wrapper checked the three DLLs against the frozen build both before and after execution; packaging rechecked their hashes. The native exit code is 0. The log retains the software OpenGL/V-sync warning and environment catalog diagnostic rather than hiding them.

## Reproduce this archive

From this directory's parent, with the original raw capture and final DLL directory available:

```sh
python package-native.py --repo "$REPO" --run-dir "$QA/native-idle-right" \
  --capture-dir "$QA/native-idle-right/capture/idle-20261110-224943-001" \
  --assemblies "$QA/final-assemblies" --kind idle-right \
  --crop 448 248 505 316 --output "$OUTPUT"
```

The recovery recipe passes its exact confirmed route. Set `OUTPUT` to an empty destination for independent reproduction. This script packages existing observations only; it does not launch, import or rebuild the game. Python 3, Pillow, ffmpeg with `libx264rgb`, and ffprobe are required. The separately maintained `../verify-native-evidence.py` independently checks the archived video, trace, identity, save and timeline.
