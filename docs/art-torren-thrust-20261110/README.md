# Torren's first authored action slice

This change replaces **only campaign-torren's attack** with six new painted
poses: anticipation, early extension, near extension, full thrust, withdrawal,
and recovered stance. It does not complete the requested whole-character or
whole-campaign animation set. Idle, two-pose movement, hit, death and celebration
remain on their existing artwork and timing. No gameplay, damage, save format or attack-preview changes. A small native
clip option suppresses the old accent for this authored action; all other clips
retain it.

## Generation and extraction

The [sprite-gen component-row method](https://github.com/aldegad/sprite-gen)
informed identity locking, one-action row generation, connected-component
extraction, contact registration, explicit frame rectangles and small-size QA.
The actual generator was the built-in image generation tool. This is an adapted
workflow, **not a run of the sprite-gen CLI or its Grok video pipeline**. No API
key, Grok account, interpolation model or separately billed provider was used.

The reference is `godot/assets/painted-units-v1/torren-stage01-v2.png`.
The three exact prompts and all three returned rows are retained here. V1 was
rejected for weak full extension. V2 improved extension but shortened the sword
and jumped too quickly into withdrawal. V3 (`source-row.png`) restored sword
proportions, full extension and an intermediate withdrawal. Independent static
review found no obvious identity, equipment, handedness or anatomy defect.

The six poses are not equal-width cells. The extraction groups alpha >16/255
connected components, then retains original RGBA pixels within three pixels of
the nearest component. It removes 22,365 detached pixels whose maximum alpha is
5/255. It never paints limbs, interpolates frames, stretches a pose or changes
retained source pixels. Four transparent pixels surround every packed component.
`extraction-review.json` freezes source bounds, per-frame hashes and registration
measurements. `verify-extraction.py` recomputes that extraction and compares all
runtime pixels against the source; it needs Pillow, NumPy and SciPy.

The native `selected-assets.json` remains the sole owner of live rectangles,
anchors and scale. Anchors were visually registered to the planted leading boot
and the original pose's ground offset; measurements have about ±5 source-pixel
uncertainty. A single 0.1047 scale preserves approximately 35–36 map-unit body
height. No independent per-frame fitting or runtime root motion was introduced.
The established seven-unit action lunge and native contact/shadow/depth owners
remain unchanged.

`runtime-contact-1x.png` shows the baseline idle and six extracted poses at their
native body height, on a neutral diagnostic background. The 3× image is nearest
neighbor magnification only. `anchored-preview.gif` is an offline diagnostic,
not a screenshot of the game. It intentionally holds each authored drawing;
it is not smooth video or a claim of 60 unique poses per second.

## Runtime contract and verification

`torren.thrust` selects `torren-thrust-v1` only through the explicit named actor
binding. Spans are 65/50/45/65/70/65 ms, totaling 360 ms. The 160 ms release is
exactly the start of full extension. Existing actor playback and the receipt
clock sample it; the original idle sheet returns at the action's own end.
All other bindings retain their previous 150 ms attack / 75 ms release.

The initial real-render review found that the old gold crescent covered the
thrusting arm and sword. The admitted immutable `ShowAccent` clip property now
controls only accent creation. `torren.thrust` explicitly sets it false; other
clips retain the existing true default. No actor-name or frame-count heuristic
selects effects, and impact/release, clock, motion, cleanup and idle restoration
remain the existing shared composition. Native checks verify the absence of an
accent for every Torren pose while legacy accent checks remain intact.

The new production-art checks exercise all six half-open pose intervals,
transparent padding, unique source pictures, both mirrors, impact release,
contact/shadow consistency, idle restoration, cancellation before/after release,
a coarse tick, reduced motion and once-only event publication. The historical
synthetic timing fixture explicitly selects its static named action so it tests
its intended contrast independently of the new production art.

The pre-occlusion-fix native build completed with zero warnings/errors and
AnimationChecks exited 0 with 3,265 assertions. Its aggregate was deliberately
interrupted after the 16 independent level replays to correct the visually
identified occlusion; that interrupted run is not counted as a full pass.
Final native and real-render acceptance are recorded below.

Final focused AnimationChecks exited 0 with **3,279 assertions**, with zero
native error/leak diagnostics. The one-channel pixel-corruption negative check
was rejected by the extraction oracle. Formatting, indexed source companions
and diff whitespace checks passed.


## Actual native capture

The final capture uses the production `NativeCapture` scene and an isolated C01
turn-four save. Torren moves one cell and commits a normal physical attack
against the Legion Shieldguard. The game reports nine damage and six battle
events, then restores Torren's original idle appearance. No bespoke preview
logic, generated test scene or synthesized battle result is involved.

The 45 full-viewport PNG observations are preserved in
[`verification/native-thrust-lossless.mp4`](verification/native-thrust-lossless.mp4)
using lossless RGB H.264. Every decoded RGB byte was compared with all 45 source
PNGs, not just selected frames. The original PNG and RGB hashes, video hash,
assembly hash and pose sequence are in
[`verification/native-proof.json`](verification/native-proof.json). Full per-frame
native state and observed elapsed timestamps are in `verification/native-frames.json`.
The video is encoded at the fixed simulation rate of 60 Hz. GPU readback slowed
wall-clock observation, so neither it nor the startup FPS observation is a
performance benchmark or a claim of 60 different authored poses per second.

- Frames 0–8: the unchanged move pose.
- Frames 9–30: all six authored attack poses, in order.
- Frame 19: full-extension release, without the obsolete gold actor accent.
- Frames 31–44: the original idle sheet; the final frame has no active presentation.

[`verification/native-release.png`](verification/native-release.png) is the full,
unmodified release viewport. `verification/native-phases-2x.png` is a labeled,
nearest-neighbor crop of the six action phases and idle restoration. Independent
review found matching identity/equipment/style, readable arm extension, no
obvious scale jump or position teleport, and no remaining blocking art defect.
The final leg-stance change is visible and is not claimed to be fluid continuous
motion. The white target-hit effect remains below the target; it does not hide
the authored core attack as the removed actor accent did.

The final native session closed normally after recording and its complete log
has no ERROR or leaked-object diagnostic. The graphics driver's unsupported
V-Sync warning and the existing environment `runtimeReady=false` diagnostic
remain disclosed in `verification/native-session.txt`. Two preceding launches
had shader-cache / XServer diagnostics and are not counted as clean acceptance;
the final session used a fresh isolated desktop-side cache and configuration.

At this checkpoint the full aggregate native gate is still running. The focused
checks and actual captured action have passed; final aggregate results will be
added after its process and teardown complete. This is one accepted authored
physical-attack slice, not the remaining idle, magic, death or celebration work.
