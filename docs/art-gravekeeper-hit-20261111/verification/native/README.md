# Gravekeeper hit: actual native UI evidence

Both `hit-right/` and `hit-left/` contain completed native motion captures from the frozen tested source. Each archive has 50 original 960×640 observations at stride 1, encoded once at 60 fps. The first-to-last observation span is 0.816667 simulation seconds; video duration is 0.833333 seconds. These are sampled simulation observations, not wall-clock performance measurements.

The legal witness is an ordinary AI archer 18 command: one movement cell, then a nonfatal 33-damage hit on keeper 4, reducing HP from 37 to 4. The normal next AI action is endTurn. Each complete official F5 output matches the legal settled result after removing only `campaign.savedAt` and `battle.savedAt`; both writer timestamps changed. Right-facing and left-facing keeper orientations are preserved throughout each capture.

## Contents

- `hit-right/` and `hit-left/`: lossless RGB movies, original JSON trace objects as JSONL, selected untouched PNGs, exact nearest-neighbor 3× inspection sheets, startup title-screen captures, original input/output saves, source/DLL/log/exit evidence, save-comparison reports, proofs and per-archive checksum manifests.
- `frozen-identity.json`: expected source commit, source trees, DLL hashes and legal witness file hashes.
- `package-native.py`: packages existing completed captures; it does not launch, import or build the game, or edit game state.
- `verify-native-evidence.py`: reads raw captures or portable archives; checks source identity, legal saves, recorded timeline, native exit/logs and every decoded video RGB frame, selected PNG and crop tile.
- `test-native-evidence.py`: 40 isolated controls plus optional detached-copy tamper controls against a real archive. Synthetic and deliberately corrupted copies are never presented as native evidence.
- `*-raw-verification.json`, `*-archive-verification.json`, `*-tamper-controls.json` and test/package logs: completed verification results.
- `VISUAL-REVIEW.md`: actual sampled-image review, scope and limitations.
- `EVIDENCE-SHA256.txt`: recursive checksums for this portable directory, excluding the checksum file itself.

Raw PNG sequences remain outside Git. Every raw PNG byte hash and raw RGB hash is retained. The videos preserve RGB exactly, not PNG alpha. Every original trace object is preserved byte-for-byte, with one LF separator added per object. Original native logs, manifests and comparison reports retain engineering workspace paths as provenance; the scripts never require those original paths.

## Source and observed timing

- Source commit: `27de886c111600c64a868f9d57fc6868a6db45e4`.
- Godot tree: `02b18b80aff45880bb58e503574b858c921449aa`.
- Tools tree: `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`.
- Source durations: 110 ms per AI movement cell, 320 ms archer release, 600 ms archer recovery, 320 ms keeper recoil and 420 ms impact. The 160 ms human-movement branch does not apply to this AI witness.
- Both captures have keeper recoil poses 0, 1, 2 and 3 first visible at zero-based observations 24, 27, 33 and 39. Recovered idle pose 0 appears at observation 43. The first completed presentation observation is 49.
- Archer shot poses 0 through 5 start at observations 5, 11, 17, 24, 29 and 35. Archer breathing resumes at observation 41.

No observation count was assumed before capture. The first captured movement frame may occur after movement starts. Boundary compatibility is checked at 60 Hz, including the source release and finite recoil/impact intervals. Raw traces do not measure effect opacity or expose effect-node lifetimes; source contracts and authored-art tests cover those properties separately.

## Portable verification

Python 3, Pillow, ffmpeg with `libx264rgb`, and ffprobe are required. The independently verified legal package must be available as `$LEGAL`; its exact expected files are hashed in `frozen-identity.json`. The `archive-legal` package supplied with this batch has matching bytes.

```sh
sha256sum --check EVIDENCE-SHA256.txt
python test-native-evidence.py
for kind in hit-right hit-left; do
  python verify-native-evidence.py --kind "$kind" --identity frozen-identity.json \
    --proof "$kind/native-proof.json" --traces "$kind/native-frames.jsonl" \
    --video "$kind/native-lossless.mp4" --input-save "$kind/native-input.json" \
    --output-save "$kind/native-output.json" --legal-root "$LEGAL"
  python test-native-evidence.py --archive "$kind" --kind "$kind" --legal-root "$LEGAL"
done
```

Each archive README contains its confirmed UI route and packaging command. Store new verification reports outside the per-capture directories, whose manifests intentionally cover exactly their artifact files. Use a new empty destination when repackaging. SHA-256 detects changes relative to the trusted frozen anchor and manifests; it does not prove authenticity if an adversary replaces both the evidence and every trusted anchor.
