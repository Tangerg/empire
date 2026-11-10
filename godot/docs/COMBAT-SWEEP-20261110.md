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
- Full `verify-native.sh` validation subsequently completed on this new sweep,
  as recorded below. The earlier pass in
  [the recovery report](NATIVE-SCRIPT-DIAGNOSTICS-20261110.md#completed-recovery-run)
  belongs to the prior implementation.

## Completed full native gate

`bash tools/godot/verify-native.sh` completed on implementation
`6afcf7a267fbaadb8bedc3f1370843dce77510da` with aggregate exit **0**, a recorded
terminal status, and the final aggregate success marker. The source tree stayed
unchanged throughout this run.

- Wrapper and format-selection regression tests, formatting, source metadata,
  authored-scene guards, builds and editor import passed.
- All 17 core groups and all 16 independent level replay/save checks passed.
  The 3,425 ordered action/event/receipt traces match the immediate pre-change
  complete gate exactly.
- All 31 native scenes passed the strict exit/diagnostic boundary, including
  the 3,178-assertion animation scene.
- Normal automatic playback completed the same AI defeat/retry, 415 legal orders
  across two carried victories, 1,108 once-presented victory-battle events and
  21 orders into the third opening. Terminal battle/campaign states match the
  pre-change run. The played-flow scene reported 20,145 assertions; its frame
  polling contributes a variable count, not a different rule outcome.

Retained evidence: `verification/combat-sweep-20261110/full-native-passed.txt`
and `full-native-status.json` (source commit, UTC bounds, actual exit, hashes and
comparison summary). This is a full native development gate, not a new exported
release or full-campaign victory claim. No GitHub commit statuses or PR-triggered
workflow runs were reported for this implementation when checked; no remote CI
pass is inferred from the local result.

## Limited actual desktop review

The existing compiled implementation was launched in the connected cloud desktop
using official Godot 4.6.3 .NET and a fresh isolated user-data directory, without
building or importing again. The review entered a new campaign, chose the north
hill opening, selected Torren/Laiya, previewed movement and advanced normally to
C01 turn 3 at 1180×812.

Actual pixels showed the menu, story, map, houses/trees, units and Chinese HUD
without missing textures or obvious text clipping in these observed states.
`verification/combat-sweep-20261110/visual-c01-1180x812.jpg` is the unchanged CUA
window screenshot (JPEG, 1180×812), not an illustrative reconstruction. The GUI
used the same `Empire.dll` SHA-256 recorded in the status JSON.

The screenshots did not reliably capture the 150 ms melee anticipation, release,
follow-through, double trails, or their moving terrain overlap. The limited
static review therefore **does not close animation visual acceptance**. A
reliable real-render temporal capture at normal gameplay zoom remains required.
The isolated game window was left available for inspection; existing user saves
and the concurrently running verification namespaces were not touched.
