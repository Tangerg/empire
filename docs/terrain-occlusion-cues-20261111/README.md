# C10 terrain-covered unit cues (2026-11-11)

## Witness and cause

Real 960×640 C10 opening, entered through the normal staging screen using an
explicitly labelled isolated level-entry fixture, not a continuous campaign victory.
Several allies behind the central mountain were almost invisible; ready-unit
navigation could select them, but their bodies provided no practical pointer target
and no independent occlusion cue appeared.

A read-only native probe of the registered C10 opening sampled opaque actor pixels
at a two-source-pixel stride and queried the production board picker. Tasha had
0/1,111 body samples selectable, Laiya 178/16,839, Torren 4,274/17,368, and the two
old-banner units 0 selectable samples. These are source-image samples, not screen
pixels or a performance benchmark. The probe used production rendering; its
one-off instrumentation was restored and is not a Luojia suite pass.

C10 uses TerrainView standing sprites. Their default pointer contract receives
input, but the cue algorithm counted only foreground materials that *did not*
receive input. This incorrectly coupled visual occlusion to pointer routing.
Correcting only that condition exposed a second problem: the old small local
candidate grid could not escape the mountain footprint for nine actors.

## Change

- Count opaque foreground material whether or not it receives pointer input.
- Search the nearest safe 24-pixel lattice point across the bounded field, rather
  than abandoning the cue after the small local search. The actor-relative grid
  and stable row/column order make placement deterministic.
- Keep the existing marker, owner colour, leader line, collision exclusion,
  frame policy, fog filtering, animation suppression and original material hits.
- This does not raise actor Z order, erase terrain, reveal fog-hidden actors or
  make mountain pixels select the actor behind them. Only the explicit cue is a
  new reachable target. Canonical state, movement and attack rules are unchanged.

The search is a finite lattice search, not a proof that no continuous free point
exists. Unplaceable cues still produce an explicit diagnostic. Pure projected
surface/cliff occlusion is not newly implemented in this slice.

## Verification

- Replacing production occlusion code with the previous version while keeping the
  new tests fails on C10 Laiya's missing cue; strict wrapper exit 1. Restored code
  passes. The earlier condition-only experiment also failed on Torren, exposing
  the incomplete placement search; it was not shipped.
- PresentationChecks uses actual C10 content/assets and verifies six covered
  allies have visible exact-unit targets, all cues find safe positions, and state
  is unchanged. Three identical snapshot rebuilds preserve all cue identities and
  positions; full-board rebuild observations were 74.38, 62.92 and 61.30 ms in
  this run (not isolated cue cost or frame time). Its existing all-chapter renderer
  checks also pass.
- DynamicStateChecks: 750 assertions. New synthetic pointer-blocking cover tests
  verify a cue, unchanged Object hits through original covered body pixels,
  fog exclusion and no cue for the same cover placed behind the actor.
- PaintedSurfaceChecks passes, retaining cliff/flat-surface exclusion boundaries.
- The first AnimationChecks invocation used native-process.sh's standalone
  300-second default and timed out (exit 124), producing forced-shutdown errors.
  It is not a pass. The rerun uses verify-native.sh's normal 900-second per-process
  budget without changing assertions or diagnostic checks.
- Real GUI after repair: blue allied cues and red enemy cues are visible outside
  terrain at 960×640. Clicking two distinct blue markers selected Laiya (116/116)
  and Torren (98/98), respectively, without advancing the turn. Mountain artwork
  continued to cover their bodies. Shared desktop released after verification.

Small text logs accompany this record. No screenshots, saves, credentials or
previously withheld evidence are included. The full verify-native.sh gate
was not rerun; this is focused rendering/interaction regression, not a complete
campaign playthrough or a frame-time optimization claim.
