# Native archer death observation

The unmodified NativeCapture scene ran the final `90952ac7` implementation at
960×640, with an isolated official save, fixed 60 Hz simulation and normal AI.
No camera, facing, HP, order or receipt was patched. The legal derivation and
complete before/after save checks are in `../legal-death/`.

At the home screen F8 armed next-transition capture, and F9 loaded the witness.
The enemy archer naturally shot east-facing Elin. Its AI then ended the turn.
After capture finished, the normal human turn 15 was saved with F5 and the
settled viewport captured with F12 before normal window close. The actual F5
output equals the independent full settled save, excluding only its two writer
timestamps. No real player save was used.

## Observed lifecycle

All 93 observations are consecutive process frames with capture stride 1. Elin's
six fall drawings first appear at zero-based observations 45, 49, 53, 58, 65 and
72. The last drawing stays in place through the terminal fade; Elin is absent
at observation 92, and presentation is no longer busy. Her root and support
plane remain constant, and fall/fade sprite scale stays 0.096 without rotation
or shrink. The normal corpse marker remains after the actor is retired.

The diagnostic crop is viewport rectangle (850,400)–(915,485), enlarged exactly
3× with nearest-neighbor sampling. It includes the entire dying character and
part of the adjacent attacker's bow. It is not an edited sprite or a new source
asset. Full viewport stills preserve the terrain, nearby actors and UI context.

The trace does not contain opacity. Terminal hold and fade acceptance therefore
combine the native pixels with the controlled-clock AnimationChecks, rather
than inferring opacity from trace metadata. Tiny fingers and bowstring details
are resolved in the original source-art review, not claimed legible at map scale.
This is one east-facing legal death, not a second mirrored native recording.
Both mirror registrations and their timing are covered by controlled checks.

## Lossless archive and verification

`native-fall-lossless.mp4` encodes every 960×640 observation in RGB using
libx264rgb CRF 0 at 60 frames per second. Immediately after encoding, every
decoded RGB byte was compared with its original native PNG. The independent
review repeated that comparison against all 93 original PNGs. The proof records
the original PNG and RGB SHA256 values, video hash, final source tree, assembly
hashes, input hash and all pose transitions. The five full stills are unchanged
PNG copies. All 93 original trace records are in `native-fall-frames.jsonl`.

Run `python3 verify-native-evidence.py` in this directory to verify the archived
video, every RGB frame, all stills and contact-sheet cells, plus the recorded
actor lifecycle. This requires Python, Pillow and ffmpeg. The legal package
contains the independent save verifier and repeatable native launch procedure.

Readback affects wall-clock pacing. The original observations span roughly 4.3
seconds while the fixed-step video plays 93 frames at 60 Hz. Neither the video
nor the observed FPS is a real-time performance benchmark. Full campaign and
standalone export acceptance are not covered here.
