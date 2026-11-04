# Protect the manual save on the title screen — 2026-11-04

## Reproduced player failure

From the unchanged shipped campaign, the native application followed New, the
opening story and choice, first-battle staging, and Start. A legal battlefield
move created a distinct played state; native F5 saved that exact battle. The
application was then unmounted and freshly constructed, simulating a relaunch
against the same isolated save files.

On the title menu, native F5 overwrote the real manual battle checkpoint with the
fresh, unstarted prologue held internally by the new application. The saved mode
changed from `battle` to `campaign`. After dismissing the success notice, native
F9 opened `StoryScreen`, not the saved battle. The targeted original-code run
failed the precise preservation assertion with exit 1; its existing opening
navigation group passed.

This is not an autosave or serialization failure. The title screen displays the
clean autosave as a Continue offer, but no campaign view has been admitted yet.
Its hidden application campaign is not a player-selected game to save.

## Ownership and repair

`GrayFlagGame.Save` owns admission to the manual-save operation. It now rejects
saving while its current view is `CampaignMenuScreen`, before serialization,
filesystem work or a success receipt. It uses the existing authoritative view,
not a new active-game flag, copied campaign or competing location model.

F9 on the title screen remains available. F5/F9 in story, choice, staging, battle
and camp retain the existing full manual-slot behavior. Clean autosaves, Continue,
manual-load confirmation policy, save schema, local story-beat reset, undo history,
map assets, campaign content and game rules are unchanged. Actual storage failures
still use the existing explicit error boundary and localized feedback.

## Native regression

The new campaign-navigation group includes:

- Exact battle save via ordinary native F5 after a real legal move.
- Fresh application/title F5 preserving the manual bytes and timestamp, followed
  by native F9 restoring the exact played battle.
- Positive F5/F9 round trips for admitted story, choice, prebattle staging and
  camp, including exact campaign/pending-battle state and location.
- Returning from camp to title without saving the retained invisible campaign or
  showing a false save-success receipt; title F9 still restores the camp slot.
- Title F5 with an absent manual slot creating no unstarted save.
- Separate automatic-slot bytes/timestamp preservation and fixture cleanup of
  both slots, all under a fresh `/tmp/empire-*` Godot user-data namespace.

The targeted RED/GREEN runner temporarily selected the existing opening group
and new admission group only. The committed runner retains every original group
and adds this one; full validation results are recorded below once complete.

## Validation status

The original-code native RED failed only the new preservation group (exit 1).
The targeted repair passed both selected groups, 91 assertions, exit 0. Review
then strengthened the initial-story positive control to require creating an
absent manual slot; the full current-native gates will also run that final version.
No incomplete stage is counted as passed. This is native input/control/storage verification in
headless Godot, not a physical keyboard, GPU visual or exported-binary acceptance.

## Godot references

- [Input event propagation](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html): native key events reach the existing application handler after GUI processing.
- [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html): the application owner mediates view lifecycle and persistence, without moving campaign decisions into presentation widgets.
