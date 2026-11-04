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
and adds this one.

## Completed validation

Code checkpoint: `7d852147e6d298b63ab7888642b68c3d9ea7109d`.

- Original-code native RED: only the new preservation group failed, exit 1.
- Targeted repair: both selected groups passed, 91 assertions, exit 0. Review
  then strengthened the initial-story positive control to require creating an
  absent manual slot; the complete navigation run below includes that final test.
- All 39 stages completed with recorded exit 0, including the unchanged commands
  from `tools/godot/verify-native.sh` and an additional ExportRelease build.
- All 25 native scenes exited 0 with the existing strict error/leak/teardown scan
  clean. Complete campaign navigation passed 7,987 assertions and every group,
  including the new save admission, failed storage/admission, actual played first
  victory, carried continuation, and formal Redstone resume flows.
- Debug and ExportRelease builds: zero warnings/errors. Evaluated ExportRelease
  compilation includes the application guard and excludes navigation-check files.
- Thirteen core contracts passed, including all 16 independent formal levels:
  3,425 legal native orders with exact save/replay round trips. These results
  include losses and turn limits, not a claim of a carried-state campaign victory.
- Objective/outcome: 141 assertions; catalog ownership: 76; camera integration:
  313; HUD: 459; dossier focus: 73; battle-result flow: 371; campaign UI: 205;
  lighting lifetime: 106; scenery/resource/lifecycle: 8,753.

The full driver started at **2026-11-04 01:44:44 UTC** and completed at
**02:22:41 UTC**, with final driver exit 0. The environment restarted afterward.
Recovery verified all 39 terminal stage records, the final completion marker,
exit code, unchanged script/binary digests, clean worktree and retained commit.
No stage was interrupted or inferred to have passed; none needed repeating.

An earlier preparatory editor import exited 0 but printed a Godot shutdown-time
`EditorSettings` diagnostic for `export/android/android_sdk_path`. Its log was
retained. The original import command ran again in the full driver and exited 0
without that diagnostic. Existing authored `runtimeReady=false` diagnostics are
also retained rather than waived or removed.

This is native input/control/storage verification in headless Godot, not a
physical keyboard/touchscreen, GPU visual or exported-executable acceptance.
No artwork, save migration or rule change is part of this repair.

## Godot references

- [Input event propagation](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html): native key events reach the existing application handler after GUI processing.
- [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html): the application owner mediates view lifecycle and persistence, without moving campaign decisions into presentation widgets.
