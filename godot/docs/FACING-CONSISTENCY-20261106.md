# Facing consistency, 2026-11-06

## Reproduction and ownership

A genuine C04 receipt was reproduced in the displayed native game with the old
formal gravekeeper sheet, not the unaccepted animation candidate. After 91
verified legal prefix actions, unit 9 moved `(25,16) → (25,17)` and attacked
`(25,15)` with `c01.lantern-staff`. The receipt was `west → south`, movement,
`south → north`, activation, damage 23, rank progress, completion. C04's registered
plane gives north/south zero horizontal projection.

Before the fix, playback retained the previous left mirror through completion,
then full settlement rebuilt the actor unmirrored. The rule/state was correct.
Playback ignored `facingChanged` and used raw screen-direction signs with a
special preserve-previous branch for zero. That branch disagreed with settled
projection, and raw signs also disagree with square4's dominant-axis quantization.

The fix consumes the ordered facing receipt and projects the supplied grid's
`Toward` answer for temporary aim and each movement segment. Continuous lunge
geometry remains independent. Normal recovery restores the pre-action mirror,
which matters for counter/support/parting attacks and due delayed casts that do
not change canonical facing. No rule, save schema, map, atlas or art binding changes.

The checked-in `tests/native/c01-04-facing.json` retains the exact seed,
91 prefix actions and every intermediate state hash. `AnimationFacingChecks`
replays that legal sequence with the current engine and actual registered C04
scene, then guards first movement, both facing receipts, stationary release,
recovery before impact ends and final settlement. Its controlled timeline is an
automated correctness regression, separate from the native observation below.

## Native original-art comparison

Both runs used the same standalone C04 legal witness, real `BattleSession`
dispatch, actual `BattleScreen`/HUD, automatic native presentation clock and old
formal sheet. Every prefix action/event/state hash was checked. This was not a
manually advanced timeline or an injected victory.

- Before state SHA-256: `0116c88bcaf4b0ac08d5b7bf2c41e2755c2a8259213444edf07e4b34e5d63a84`.
- After state SHA-256: `3a828e23dff19066d1facbdd12b99b8056b0bb9e9d17c37b3bb9bab2e28c6a5e`.
- Action, ordered event payloads and both state hashes are identical before/after.
- Old code: movement, activation, impact and recovery left; settlement right.
- Fixed code: first movement frame, activation, impact, recovery and settlement
  all right. Native process exit 0; every receipt event published once in order.

A first fixed-code native run exceeded the review harness's 30-second wall-clock
bound while a heavy headless suite was also running. It was retained as a failure,
not accepted. One serial rerun of the identical command and unchanged native binary
completed with exit 0. This does not prove the cause of that timing outlier.
Software-rendered llvmpipe and unavailable audio remain environment limitations;
wall times are not a performance comparison or a claim of smooth visual acceptance.

## Verification status

The final focused `AnimationChecks` run passed **2,920 assertions** and the
strict native process/diagnostic boundary returned **0**. The unchanged original
`bash tools/godot/verify-native.sh` then completed uninterrupted in
**3266 seconds**, exit **0**, against implementation commit
`23a4e544e21b3e351f67c267c587a66a85673a51`. All 31 native scenes, both builds,
format/metadata/static-map checks, core contracts, 16-level replay/save,
objective/catalog checks and the final played campaign flow completed. All 16
AI action/event/receipt digests are identical to the preceding `8d1991695`
checkpoint. The final verification commit changes reports only.
Controlled negatives already compile successfully and are rejected at the intended
new assertion: restore the entire old production playback, omit recovery mirror
restoration, ignore the ordered facing receipt, omit initial path facing, or
replace grid quantization with the raw target vector. Fixed sources are restored
after each negative; no mutant is part of the implementation. The
original headless pass with pre-fix expectations stopped at the obsolete
expectation that a temporary aim should persist after recovery. That assertion now
requires restoration to the canonical pre-action mirror, without weakening crop,
anchor, scale, shadow, contact or depth checks. The first new-suite run also
caught a fixture setup error: the hex guard queried `Projection` immediately
after reconfiguration, before rendering its snapshot. The fixture now renders
its canonical state first; no production fallback or relaxed assertion was added.

New candidate art remains review-only. This patch does not accept its aesthetics,
continuous movement quality, all scenery occlusion, audio or standalone exports.
