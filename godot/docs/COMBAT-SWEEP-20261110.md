# Continuous melee action sweep

Baseline: `0b7b896951a746c3e109d2c6d7ff717fa0cf9303`.

## Change and ownership

The existing compact melee ribbon now moves continuously from anticipation,
through the target-aligned release, into follow-through. Its angular offset is
-0.65 radians at the beginning, zero at release and +0.65 at clip end. Two thinner
tapered ribbons appear only during recovery, behind the leading sweep and fading
with it. Every shape is recomputed from the current clip sample, so a coarse frame
cannot leave a different historical trail or start another effect clock.

`ActorActionAccent` remains an actor-owned child. The same committed receipt and
admitted clip own its timing; the existing playback still owns cancellation,
replacement and final settlement. Standing depth, canonical foot/contact,
mirroring and actor-frame ownership are unchanged. No game rules, event order,
damage, release marker, clip length, camera, save schema or HUD forecast changes.
The current shipped action still uses its trusted one-frame, 150 ms pose.

Stationary weapon actions and healing keep their neutral upright focus diamond.
No weapon or damage-type inference, new sprite sheet, particle system or additive
bloom is introduced. This is motion/feedback refinement, not newly authored
character animation.

## Verification scope

Native checks cover the exposed node transform and lifecycle, not final pixels:

- Left/right, vertical/diagonal and zero aim.
- Anticipation, immediately before/at/after release, and mid-recovery.
- Continuous forward sweep through release, with the same existing impact node.
- Identical transform from coarse and subdivided clock steps.
- Cancellation during recovery and no effect retained by the next projection.
- Finite zero/end release boundaries and huge finite recovery.
- Upright stationary focus and the existing reduced-motion, visibility,
  reentrant replacement, hit-number and actor-cleanup checks.

The ribbon drawing and its color/alpha were code-reviewed, but the native
transform assertions do not inspect drawn ribbon pixels. Removing the trail draw
calls would not be detected by those transform checks. Real-display review of
normal-speed melee at gameplay zoom and behind overlapping terrain remains open,
as do current exported-package acceptance and new character artwork.

## Completed focused checks

- The new sweep assertions ran against the unchanged production accent and
  failed at `Lunge wind-up starts behind the projected aim...`. Compilation
  succeeded; the native scene and strict wrapper returned 1 for that actual
  assertion. See `verification/combat-sweep-20261110/baseline-negative.txt`.
- After the implementation, native compilation passed with zero warnings and
  errors. `AnimationChecks` passed **3,178 assertions** (51 more than the
  baseline), with normal exit and the strict diagnostic wrapper. Build and scene
  logs are retained beside the negative control.
- Formatting, script UID/resource metadata, and `git diff --check` passed.
- The final implementation was reviewed for normalized large-time arithmetic,
  release-only trails, positive ribbon radii, aim ownership and cleanup.
- Full `verify-native.sh` validation of this new sweep is pending at this
  checkpoint. The complete pass recorded in
  [the recovery report](NATIVE-SCRIPT-DIAGNOSTICS-20261110.md#completed-recovery-run)
  belongs to the prior implementation, not this change.
