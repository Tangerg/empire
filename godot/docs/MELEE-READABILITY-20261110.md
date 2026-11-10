# Melee trail readability at gameplay zoom

Baseline presentation: `6afcf7a2`, documented by `be6b6b2a`.

## Observed problem and change

An actual C01 turn-4 Torren command, moving one cell and using the border long
sword against the north shield, was entered through the native GUI in an isolated
save namespace. At 1180×812 and the normal 1.25 camera zoom, the leading crescent
read clearly but its two thin inner trails were weak against the dark figure.

The trails now use 55% rather than 35% of the leading ribbon's sampled opacity,
with a one-pixel edge in the existing ink color. The common ribbon drawer owns
both fill and outer edge. The leading ribbon's geometry, colors and outline are
unchanged. The added edges make the layered follow-through easier to distinguish
without new bloom, particles, sheets or full-screen effects.

This changes drawing only. The same admitted 150 ms action and 75 ms release,
clip clock, seven-pixel approach, standing depth, cancellation and settlement own
the result. There are no battle, save, attack-preview or damage changes.

## Real-render capture and its limits

The first ordinary-clock capture retained the complete command in nine frames,
but readback intervals of 43–177 ms left only two action-pose frames. That capture
cannot establish smoothness or visual quality throughout the 150 ms action.

For inspectable before/after evidence, the official Godot 4.6.3 .NET executable
was then launched on the connected X11 desktop with `--fixed-fps 60`, the actual
`NativeCapture` scene, Dummy audio, and the same isolated manual save. Normal GUI
input selected the same character, destination and attack in both runs. The
application used its own committed receipt and normal automatic presentation;
no synthetic battle event, alternate actor sheet, altered time scale or test
presenter was substituted.

Both captures contain 40 real rendered frames. Frames 9–17 cover the nine action
samples from 6.67 to 140 ms, after the one-cell 160 ms move. Release is between
frames 13 and 14. The unchanged emitted damage is 9. Frame 18 has returned to
idle, and frame 39 records completed presentation.

**Fixed 60 Hz simulation steps are not an observed 60 FPS performance result.**
The software-rendered desktop/readback took longer in wall time. JSON `elapsedMs`
is observed wall time, not clip time. The numbered frame sequence provides the
fixed-step visual comparison. The contact sheets are labeled, nearest-neighbor
2× crops of those real frames; the retained full release images are unmodified.

The opt-in capture buffer was raised from 64 to 256 MiB: 40 RGBA frames at this
size require 153,305,600 bytes, while the old cap fits only 17 frames. Its hard
abort, image disposal, no-mixed-transition guard and deferred PNG compression
remain intact. `NativeCapture` remains excluded from ExportRelease.

## Comparison and checks

Evidence is in `verification/melee-readability-20261110/`:

- `contrast-comparison.png` shows baseline above revised recovery, frames 14–17.
- Before/after contact sheets cover anticipation, release and recovery; full
  release frames retain the complete gameplay layout.
- The two frame-trace files include all 40 original PNG hashes and all actor
  transforms/frame indices. Every actor trace and presenting flag matches.
- Within the inspected 160×110 combat crop, only recovery frames 14–17 differ.
  Differences above two channel levels affect 272, 300, 317 and 289 pixels,
  respectively. Anticipation, the resumed idle, hit placement and remaining
  damage-number lifetime are unchanged in that crop. `pixel-comparison.json`
  retains the bounds/counts; these are evidence for this capture, not a general
  screenshot regression suite.
- Native compilation passed with zero warnings/errors. The strict native
  AnimationChecks process exited 0 after all 3,178 assertions. Formatting,
  indexed source metadata and `git diff --check` passed.
- Full `verify-native.sh` is running on the unchanged final source. Its outcome
  is not yet claimed at this checkpoint.

This closes the lack of temporal pixels for this C01 long-sword example. It does
not establish all-direction, counterattack, healing/ranged, moving-terrain
occlusion, hardware performance, exported-package or complete-campaign visual
acceptance. The headless animation checks cover timing and lifetime, not the
appearance of draw calls. The improvement is intentionally modest; the shipped
single action pose is still not newly authored character animation.
