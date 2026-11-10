# Actual native gravekeeper breathing and recovery evidence

These three completed native UI runs contain 254 actual 960 × 640 framebuffer
observations from the same frozen tested build. They use isolated official saves,
normal game controls and unchanged production presentation logic. The complete
raw PNG sequences remain outside Git. The archive preserves every observation as
lossless RGB video, every original actor trace and per-frame hashes, selected
untouched full screenshots, labeled nearest-neighbor diagnostic contact sheets,
and complete native run/save/build provenance.

## Recordings

- [Right-facing idle](idle-right/README.md): 103 observations, stride 3, encoded
  at 20 fps. First-to-last span is 5.1 simulation seconds; container duration is
  5.15 seconds. Keeper 4 is unmirrored. All four breathing poses and two loop
  wraps are visible with the root and support plane fixed.
  [Video](idle-right/native-lossless.mp4) ·
  [Four-pose inspection sheet](idle-right/native-keeper-poses-3x.png).
- [Left-facing idle](idle-left/README.md): 103 observations, stride 3, encoded
  at 20 fps. First-to-last span is 5.1 simulation seconds; container duration is
  5.15 seconds. Keeper 4 is mirrored. All four poses and two loop wraps are
  visible, with the same planted character and equipment.
  [Video](idle-left/native-lossless.mp4) ·
  [Four-pose inspection sheet](idle-left/native-keeper-poses-3x.png).
- [Left-facing cast recovery](recovery-left/README.md): 48 observations,
  stride 1, encoded at 60 fps. First-to-last span is 47/60 simulation seconds;
  container duration is 0.8 seconds. Cast poses start at observations
  000/005/011/017/023/030, the source restores breathing pose 0 at 035 while
  `presenting=true`, and the receipt completes at 047. This is the full native
  source/recipient presentation window for a legal 30 HP heal, not an idle-only
  recording.
  [Video](recovery-left/native-lossless.mp4) ·
  [Cast-to-idle inspection sheet](recovery-left/native-keeper-transitions-3x.png) ·
  [Untouched recovery frame](recovery-left/native-observation-035.png).

All three runs used the engine's configured 60 simulation frames per second.
Encoding uses the corresponding observation rate, without synthesized,
interpolated or duplicated frames. Every decoded full-frame RGB buffer equals
its original native PNG RGB buffer byte-for-byte. The H.264 RGB video does not
retain PNG alpha. Pixel readback affects frame pacing: elapsed trace timestamps
and playback speed are not a wall-clock performance benchmark.

## Interpretation and limits

The trace retains capture/render counters, observed elapsed time, `presenting`,
actor art and frame selections, facing, uniform scale, registration offsets,
positions and support planes. It does not record opacity or recipient effect
events. Source 600 ms recovery and recipient/receipt 800 ms completion come from
production timing contracts and controlled-clock tests, supported by the native
sequence. Sampled footage alone does not establish exact sub-frame timing.

Visual inspection of the two pose sheets, the recovery transition sheet and the
untouched full recovery screenshot found four distinct subtle breathing poses,
complete shovel/lantern/boots, planted feet, and an acceptable cast-rest to idle
handoff. The recipient's +30 effect is still visible when the source returns to
idle. These are presentation observations, not claims about unrecorded state.

The 3× sheets contain only labeled crops from actual frames, enlarged by nearest
neighbor. They are diagnostic aids, not new artwork or composited game views.
Each proof records crop coordinates, observation indices and layout, so every
tile can be checked against the decoded full-frame movie. `native-startup.png`
in each run is the automatic startup title-screen capture, not a settled
battlefield. Live pre-recording battlefield inspections were not saved as
separate native PNGs.

## Provenance and saves

- Production commit: `869078976dd21f5d82932dc4151bdd03fa973c1a`.
- Godot source tree: `cfe370846c78c10100fbcfc8d19965efd96be697`.
- Tools source tree: `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`.
- `Empire.dll`, `Empire.Core.dll` and `GodotSharp.dll` were checked against the
  frozen build before and after each native run, and rehashed during packaging.
- Every native process exited 0 and retained its complete log. The log includes
  the software OpenGL/V-sync warning and environment catalog diagnostic.
- Both complete idle F5 outputs equal their legal input saves, except the two
  changed save-writer timestamps. Recovery equals the independent legal heal
  output, except the same two timestamps. Target 3 changes from 39 to 69 HP;
  keeper 4 faces west. No receipt, state, facing or camera was patched.

Each package has the actual input/output saves, raw source and DLL hash records,
normalized portable DLL hashes, native log/exit, full-save comparison, and
`native-proof.json`. The proof includes SHA-256 hashes for every original PNG,
original trace, decoded RGB frame, video and provenance file. Legal source and
replay evidence is in [../legal](../legal/README.md).

## Reproduce and verify

[`package-native.py`](package-native.py) accepts explicit repository, completed
run, capture, frozen DLL, crop, route and output arguments. It packages existing
observations only and never launches, imports or builds the game. See each run's
README for its exact source capture and crop. Requirements are Python 3,
Pillow, ffmpeg with `libx264rgb`, and ffprobe. Use a fresh output directory to
reproduce without altering the checked-in evidence.

The separately maintained [`verify-native-evidence.py`](verify-native-evidence.py)
checks both raw captures and archived packages. See [VERIFIER.md](VERIFIER.md)
for the independent method, controls and timing limitations. From this directory:

```sh
for kind in idle-right idle-left recovery-left; do
  python verify-native-evidence.py --kind "$kind" \
    --proof "$kind/native-proof.json" --traces "$kind/native-frames.jsonl" \
    --video "$kind/native-lossless.mp4" \
    --input-save "$kind/native-input.json" --output-save "$kind/native-output.json" \
    --source "$kind/native-source.txt" --hashes "$kind/native-assemblies.sha256"
done
python test-native-evidence.py
```
