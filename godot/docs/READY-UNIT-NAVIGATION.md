# Ready-unit navigation

## Player-facing gap and reference

At baseline `5dc64dc7`, `TacticalSelectionController.Key(" ")` could cycle
ready actors and center the camera, but `BattleScreen.UpdateTactical` mounted
only End Turn and Undo. Pointer-only players had to visually search the whole
map, and no global control disclosed how many selectable actors remained.

The [Wesnoth hotkey reference](https://wiki.wesnoth.org/HotKeysSystem)
separates next-unit navigation from ending a unit's turn. The
[WarPlan manual, section 7.3](https://www.matrixgames.com/amazon/PDF/WarPlan/Warplan_manual_printer_friendly.pdf)
also exposes previous/next-unit controls over units with operation points left.
These support a visible navigation affordance; Empire keeps its own canonical
turn rules and existing Space behavior rather than copying another game's rules.

## Contract

- The button above End Turn displays the number of currently selectable ready
  actors and locates the next one. It uses the exact same controller path as Space.
- `CycleActors()` is the single selection cohort: revision-bound canonical
  actors whose current position is inside the interactive board. The displayed
  count is a read-only projection, not a second readiness state or new turn rule.
- Mouse, touch, focused Enter/Space, and battlefield Space all select and reveal
  a unit without committing orders. The source ordering and wraparound remain.
- Wait, Undo, and external session transitions refresh the count through the
  existing revision boundary. Busy, detached, stale, and duplicate pending
  callbacks retain the existing native activation guard.
- Zero uses “暂无可行动单位”, not “all units acted”: absence of selectable actors
  does not prove that every unit has spent its action. The button is disabled.
- Deployment and computer turns do not display the player's readiness button.
  End Turn remains an explicit player choice, including when units remain ready.

The layout uses a native VBox/HBox composition, bottom-aligned within the
existing right-hand rail. It does not widen the rail into action previews.
See Godot 4.6 guidance on [containers](https://docs.godotengine.org/en/4.6/tutorials/ui/gui_containers.html)
and [keyboard focus](https://docs.godotengine.org/en/4.6/tutorials/ui/gui_navigation.html).
Normal Control focus and activation are retained; no gameplay action is bound
to Godot's built-in UI navigation actions.

## Verification

`ReadyUnitNavigationChecks` is included in `tools/godot/verify-native.sh`.
It mounts real C01 sessions and drives native mouse, touch, Enter, Wait, Undo,
and End Turn. It checks canonical ordering, wraparound, complete state/event
non-mutation during navigation, depletion and restoration, stale callbacks,
busy changes, replacement bindings, deployment, AI turns, shutdown, and full
control bounds at 960×640, 1280×720, and 1440×900.

Focused command from the repository root after build and editor import:

```sh
source tools/godot/env.sh
XDG_DATA_HOME="$(mktemp -d)" bash tools/godot/native-process.sh /tmp/ready-unit.log \
  ReadyUnitNavigationChecks "$GODOT_NET" --headless --path godot \
  res://Scenes/ReadyUnitNavigationChecks.tscn
```

Focused native verification passed 90 assertions. A negative control removed
one canonical actor from the shared cycling cohort; the check failed on the
initial readiness count. The restored implementation passed again. Native
localization checked all 1,243 catalog messages, including strict argument arity.
The full aggregate gate is pending at this implementation checkpoint.

Headless native input and geometry checks are not manual GUI or exported-package
acceptance. No current screenshot or manual device acceptance is claimed here.
