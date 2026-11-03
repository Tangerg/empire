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

Final focused and aggregate results are recorded after verification. Headless
mounted input, controls and state checks are not graphical/GPU or export acceptance.

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
