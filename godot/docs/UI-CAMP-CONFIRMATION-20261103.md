# Confirm before discarding a battle for Camp — 2026-11-03

## Player-facing problem and reproduction

The visible “战役营地” action immediately reset the current battle to its opening
state and cleared its events. That behavior belongs to the existing campaign
aggregate, but the button did not explain or confirm the loss. The same action
on the completion screen discarded an unsettled result, including a victory.

The negative native regression entered the actual C01 campaign battle, committed
a legal move-and-wait order through the screen, and requested Camp. Before repair
it failed `Camp must request confirmation without discarding the real played
battle`: the visible view and live session had already been replaced. The isolated
fixture used fresh temporary save directories, not a player's saves.

## Complete exit flow

Both live battle and completion now explain that Camp resets this battle and
forfeits its unsettled result, while existing save files remain unchanged. Cancel
receives initial focus. Cancel, Escape, or window close keep the current battle
and let the player continue, reopen the prompt, or settle a victory instead.

Only explicit confirmation invokes the existing Camp callback. It runs after the
native dialog dispatch, once, and checks the captured screen binding and lifetime.
Rebinding, removing, or replacing the screen invalidates an old prompt or queued
acceptance. Closing a prompt restores focus to the visible System entry in battle
or the Camp entry on completion, without moving the reading region.

The shared presentation helper owns only this modal lifecycle. The battle screen
and completion screen retain their own one-shot input/binding guards; completion
does not consume its navigation choice merely because a prompt was opened.
`GrayFlagGame.ExitBattle` and the campaign's reset transition are unchanged.
There is no implicit save, new save schema, changed battle rule, map edit, or art
replacement. Returning after confirmation still starts the battle from its
original opening, as the pre-existing contract requires.

## Verification boundaries

The regression suite covers the actual moved C01 battle and separate explicit
victory/defeat/draw completion fixtures. The terminal fixtures verify UI and
settlement decisions; they are not claims of a newly completed campaign route.

Coverage includes intact state/event history and both save slots on cancel,
localized warnings, safe default focus, real button signals and native Escape/close paths,
reopening, sibling-button attempts, duplicate confirmations, stale bindings,
unmount/remount, queued acceptance, and Cancel followed by victory settlement.
The complete warning is checked in a 640×360 native dialog. Escape is supplied
through the root viewport so the native embedded-window close handler receives
it; direct child-window injection was an invalid exploratory fixture and is not
counted as an accepted run.

The packaged diagnostic's existing Save/Load/Restart/Camp callers were aligned
with deferred popup dispatch, and its movement step now explicitly commits the
wait order rather than treating destination preview as a committed move. Export
smoke is not claimed: matching Linux export templates are absent in this
workspace. Compilation and source-native checks are separate evidence.

Final native results:

- Build: zero warnings/errors.
- Campaign navigation: 4,185 assertions, all 14 groups passed, including the real
  moved battle's request/cancel/confirm flow and formal Redstone return to Camp.
- Completion/result flow: 373 assertions passed.
- Campaign UI flow: 205 assertions passed.
- Camera integration: 313 assertions passed.
- Full BattleScreen real-session integration passed.
- All five final native processes exited zero without error, leaked-resource,
  segmentation-fault, or unhandled-exception diagnostics. Existing diagnostic
  warnings remain visible. Initial negative and invalid-input-fixture runs are
  retained separately, not counted as passes.
- Localization source/parser, formatter, and native resource checks passed for
  1,202 exact messages; 431 narrative/content references passed.
- Source metadata passed (154 script UIDs and 14 positive/negative cases), changed
  files pass whitespace/diff checks, and independent read-only review found no
  blocking issue.

The full formatting check was rerun and still reports 184 unrelated files, none
of the changed sources. No checks were disabled or weakened.

Current graphical/window-manager acceptance is unavailable. These checks use
native Godot controls and actual sessions headlessly. The repository-wide
formatting gate still contains the previously established whitespace failures in
184 unrelated files; this focused change does not claim a full aggregate pass.
