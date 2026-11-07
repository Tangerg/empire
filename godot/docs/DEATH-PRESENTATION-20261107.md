# Explicit death presentation phases, 2026-11-07

## Cause and scope

An isolated native death-clip review showed that the existing renderer faded and
shrunk every frame from the beginning. A terminal ground pose starting at 160 ms
in a 220 ms clip was already at 27.3% opacity. This is the intended legacy static
presentation, but is unsuitable for an explicitly authored complete fall followed
by disappearance. Candidate artwork and the isolated real-time playback have not
passed visual acceptance. They are not included in this change.

The correction makes these two presentation needs explicit rather than selecting
behavior by asset ID, number of frames, HP, a killed flag or a second death timer.

## Ownership and behavior

- The ordered committed `death` event remains the only trigger. Gameplay death,
  removal, save data, movement rules and original artwork are unchanged.
- `ActorClip` owns the authored frame intervals. Its immutable admitted death
  policy declares either `during-clip`, or `after-clip` with a positive finite
  terminal `fadeMs`. Only a death binding may carry this policy; a death binding
  must declare it. Death clips require milliseconds and forbid release metadata.
- Every shipped binding still resolves `static.death`, explicitly configured as
  `during-clip`: one trusted existing idle pose, 220 ms concurrent fade and 35%
  shrink. No extra duration is appended to this mode.
- `after-clip` plays all frame intervals without fading or shrinking, then holds
  the last frame through the declared terminal fade. Tint, starting alpha, root
  position and root scale are preserved. The same `PresentationTimeline` handles
  both stages and coarse-frame overshoot; no new timer or result flag exists.
- Both modes converge on the existing single node/standing-geometry retirement.
  Duplicate death events add no second retirement. Cancellation, rebind and exit
  settle the committed snapshot; hidden actors are filtered before art lookup;
  reduced motion remains synchronous. Receipt/result completion includes the
  terminal fade.

The native manifest and runtime ship together. This is not a save migration.
Production `selected-assets.json` is native authoring, not the historical
schema-1 TypeScript oracle export. No historical fixture, map or candidate image
is modified.

## Verification record

The first expanded `AnimationChecks` run failed in the new legal-witness harness:
it selected a recorded AI-turn death and submitted it through the player-only
`GrayFlagSession.Dispatch` API. The application correctly rejected it. The harness
now selects a recorded player-turn death using the existing session turn-status
owner. No controller, HP, event, rule or expected game state is changed. The first
failure remains recorded separately.

After that correction, `AnimationChecks` passed 3,107 assertions with a strict
native process/diagnostic exit of 0. Its C06 witness replays unchanged legal
commands, restores the actual pre-order save, dispatches the same player order
through `BattleScreen`, and verifies ordered publication, input gating, final
state, receipt and log consistency. Repeated refreshes cannot duplicate death.

The new policy checks cover invalid/missing/conflicting declarations and named
actor admission, detached input ownership, exact frame boundaries, coarse time
steps, full RGBA and root-transform preservation, duplicate retirement,
cancellation and rebinding during fall/boundary/fade, tree exit, hidden/missing
art, reduced motion, result gating and the legacy 220 ms behavior. A second
384+80 ms configuration supplements the 256+120 ms fixture to reject hidden
constant durations.

A deliberate production mutation restored opacity loss during the falling phase.
It compiled successfully, then failed at the new 31.999 ms opacity/pose assertion
with native exit 1. The unmodified production source was restored byte-for-byte.
The required full native gate is recorded in the final verification update; no
uninterrupted full-gate success is claimed before that run completes.

## Acceptance boundary

These are native resource, real-receipt integration and controllable-clock
correctness checks. Trusted existing poses are synthetic timing fixtures, not
newly accepted falling artwork. This change does not certify continuous visual
quality, candidate identity/held-object continuity, every scenery occlusion,
audio, smooth real-time rendering or standalone exports. Candidate images remain
review-only and no production binding selects `after-clip` yet.
