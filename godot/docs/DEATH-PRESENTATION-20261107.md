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
The required unchanged `bash tools/godot/verify-native.sh` first stopped after
1,637 seconds at `BattleScreenChecks`: native exit was 0, but two Godot
`Handle is not initialized` errors arose in `SwapGCHandleForType` while loading
HUD formation icons. The strict diagnostic gate correctly returned 1 despite the
scene's final pass line. Its remaining stages were not run in that attempt.

One unchanged, isolated rerun of that scene passed the strict gate. One complete
rerun of the original aggregate command then completed uninterrupted in
**3041 seconds**, exit **0**, against implementation commit
`e8f29b8671fc81c17bb92eb91f4725f7039b162c`. All 31 native scenes, both builds,
format/metadata/static-map checks, core contracts, objective/catalog checks and
16-level save/replay completed. The final animation suite passed **3,111
assertions**, including the second declared-duration fixture. All 16 AI
action/event/receipt digests match the preceding `a497afdb` checkpoint.
The final played-flow gate completed natural AI defeat/retry, both save-slot
checks, C01 and C02 victories and the inherited C03 opening. It does not claim
another complete campaign playthrough.

No production code changed between those runs. The first HUD resource-handle
failure remains unexplained, not fixed or erased by the later passes. Raw failure
and success logs are retained separately; the checked-in verification record
includes their hashes and outcomes. The final verification commit changes reports
only.

## Acceptance boundary

These are native resource, real-receipt integration and controllable-clock
correctness checks. Trusted existing poses are synthetic timing fixtures, not
newly accepted falling artwork. This change does not certify continuous visual
quality, candidate identity/held-object continuity, every scenery occlusion,
audio, smooth real-time rendering or standalone exports. Candidate images remain
review-only and no production binding selects `after-clip` yet.
