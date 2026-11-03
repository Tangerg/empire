# Tactical subjects and formation inspection

## Scope and reproduced defects

This batch changes two native presentation reads. Combat, movement, formation
rules, authored maps, artwork and save semantics are unchanged.

- **C01-09, unchanged opening (`6fefe68e`):** on the mounted native battle screen,
  cycle with Space four times to Mirelle, press Up twice, Enter, A, Right twice,
  then Enter. The chosen lantern-staff attack admits the west chain at (5, 6),
  but keyboard focus formerly classified it as ground. No forecast appeared and
  confirmation refused the order. The equivalent object pointer hit succeeds
  and deals 22 damage, reducing the structure from 100 to 78 HP.
- **C01-04, unchanged opening (`c1005293`):** inspect Laiya at (17, 20) without
  selecting an actor. The HUD formerly claimed that her defensive formation
  lacked adjacent allies. Selecting her removed that warning without changing
  state. The formation owner reports an active formation with four adjacent
  allies in both cases, and still does after an accepted in-place wait
  (`69d77f60`).

## Ownership

`TacticalSelectionController.KeyboardFocus` uses the current command's admitted
`TargetSubjects` to identify object targets. Visible units retain priority, and
the existing site/recruitment fallback is unchanged. No new structure taxonomy,
parallel legality query or keyboard-only command path is introduced. Pointer
hits remain renderer-owned: clicking ground at the same cell cannot impersonate
a structure through an occluder.

`FormationRules.Active(state, inspectedUnit)` owns whether the inspected
formation is actually in effect. `GrayFlagSession.ProjectHud` reads that fact
directly, independently of the commandable actor's formation menu. The menu
continues to use the existing `FormationRules.Options` contract. Neither read
changes battle state.

This follows Godot's separation between native GUI input and unhandled gameplay
input, and its explicit parent-provided dependencies. The existing scene/input
architecture is retained rather than adding another dispatcher. References:
[Using InputEvent](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html)
and [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html).

## Regression evidence

`BattleScreenChecks.CheckStructureKeyboardTarget` feeds actual key events through
the mounted viewport. It checks the visible 22-damage forecast, one confirmation,
and exact equality of keyboard/pointer commands, ordered events and final state.
Its ground-pointer negative case must still refuse the order.

`CheckFormationInspection` covers unselected inspection, selected inspection,
inspection after the unit acts, and a real move-and-wait to (17, 17). That last
action leaves no adjacent allies (`20d93d97`), so the inactive warning must remain.
It prevents fixing the false warning by declaring every formation active.

Both defect guards failed on their original implementations in native Godot
4.6.3.NET: the structure case had no forecast and revision 0; the formation case
displayed different active/inactive labels for the same opening state.

Verification of code commit `232ab7e10`:

- Focused format/metadata checks, zero-warning build and complete native
  `BattleScreenChecks`: normal exit 0, 2026-11-03 19:34:33–19:41:00 UTC.
- Unmodified `tools/godot/verify-native.sh`, with the existing 900-second native
  process budget and 1,200-second full-replay budget: **one uninterrupted run,
  normal exit 0**, 19:41:26–20:17:22 UTC (35 minutes 56 seconds). It includes the
  format selector's 30 boundary tests, formatting, source metadata, static map
  contracts and negative controls, builds, 13 core groups, all 16 fresh AI battles
  with save/replay checks, presentation/catalog contracts, editor import and all
  23 native scenes. No timeout or assertion was relaxed.
- The final Core assembly separately replayed the 3,379 archived commands and
  9,648 ordered events across all 16 levels, with zero new AI calls. Every receipt,
  state digest, complete final state, fixed-timestamp save and exported/native
  replay matched; runtime 107.599 seconds. Executed assembly SHA-256:
  `76c489ef2b225d2129c26428c9080f47e1feef5179ac3cc445870387874ca0f6`.
- The 16 fresh AI runs made 3,425 actions. Their per-level action counts and final
  digests match the previous version. This comparison did not compare fresh AI
  event streams; exact ordered-event parity above uses the fixed old commands.

An earlier environment restart interrupted a build before this verification;
that attempt is not counted as passed. Existing authored-art readiness diagnostics
and duplicate mouse-entry warnings in UI checks remain. There were no fatal
errors or teardown/leak failures in the passing aggregate. Headless mounted input,
controls and state checks are not graphical/GPU or export acceptance.

## Bounded related audit

No healing eligibility or healing-number discrepancy was reproduced: 18 actual
controller/session healing orders matched their recorded commands, capped HP
changes, feedback and resulting states. A further 145 reachable human-turn
status inspections matched committed statuses. This is not a new numeric healing
forecast or a claim of exhaustive status coverage.

The actual receipt formatter processed all 16 recorded formal levels, 3,379
legal commands and 9,648 ordered events without identity/formatting exceptions;
every terminal digest matched the archived baseline. Empty descriptions were
counted, not treated as defects. For example, owner-turn regeneration has no
dedicated text/animation feedback; that separate presentation omission is not
silently recast as an eligibility bug or included in this fix.
