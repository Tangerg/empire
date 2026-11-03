# Keep repeated navigation separate from deliberate orders — 2026-11-03

## Reproduced player flows

Native `Viewport.PushInput` events reproduced three unintended commands before
the repair, without changing a formal level, actor placement or combat rules:

- In C01-08, the first Enter or keypad Enter armed a legal attack by actor 5
  against actor 18. An echoed press committed that attack, advancing revision
  from 0 to 1 without another physical press.
- In C01-01, a fresh W and its repeat moved the cursor north. Fresh Enter chose
  the reachable destination (10,13) for actor 2. The next repeat of the still-held
  W became Wait and committed movement, again advancing revision from 0 to 1.

The first attack probe incorrectly assumed that C01-01 had an opening attack
opportunity. It failed during fixture setup, not at a game-defect assertion.
The corrected probe uses runtime-verified command legality in unchanged C01-08.

## Ownership and repeat policy

Godot's native adapter carries `InputEventKey.Echo` into the existing tactical
controller. The controller already owns the contextual meaning of a key, including
whether W/A means navigation or a destination command; it now owns repeatability
at that same decision point. There is no second context table in the screen.

Repeated confirmation, End Turn and Undo do nothing. Repeated A/C/H/W in a
destination menu do not choose an order or fall through into cursor navigation.
Ordinary arrow/WASD navigation, actor cycling and Back retain their behavior.
Fresh presses and explicit HUD-button requests remain deliberate inputs. Existing
busy, terminal, human-turn and revision/binding guards run before repeat rejection.

Ignored repeats do not trigger a presentation refresh. This preserves the previous
E/U no-update behavior and does not invalidate deferred focus restoration. The
native adapter retains modifier filtering and native control focus/traversal.

This consolidates the earlier per-key [Undo repair](UI-UNDO-KEY-REPEAT-20261103.md)
instead of adding another list of context-sensitive exceptions. It uses the
[Godot 4.6 repeat metadata contract](https://docs.godotengine.org/en/4.6/classes/class_inputeventkey.html#class-inputeventkey-property-echo),
not a timer, cooldown, held-key latch or assumption about operating-system repeat
speed. No map, artwork, battle rule or saved-game schema is changed.

## Verification scope

`BattleScreenChecks` protects both confirmation keys and the held-navigation
transition, including exact state/event preservation and a fresh-press positive
control that still commits one legal action. Existing Undo, End Turn across real
AI playback, modifier, focus and navigation checks remain relevant regressions.
These are native headless input checks, not desktop key-hold automation or visual
acceptance.

`CampaignNavigationChecks` also derives a real pre-victory checkpoint by playing
200 legal orders in the unchanged opening with the existing deterministic AI as
the test-player policy. Native F9 admits that checkpoint; Enter previews the
last attack, repeats cannot win the battle, and a fresh press commits exactly the
displayed actor, path, weapon and target. Every terminal event is presented before
the result screen. Settlement and ordinary story/choice callbacks then reach
`battle-02` staging with the canonical carried roster and history, without
rewriting the separate manual slot. A separate campaign instance replays the
actual UI-issued order and supplies the lifecycle reference, rather than assuming
the UI uses the same firing path as the test-player policy.

This covers one genuinely played opening victory and human-terminal presentation,
not a complete campaign victory, AI-terminal pause, or a measured animation-delay
contract. Existing synthetic ending fixtures remain explicitly labeled.

Focused validation: both projects built with zero warnings/errors;
`BattleScreenChecks`, all 73 `DossierFocusChecks` assertions, and the full
`CampaignNavigationChecks` suite passed. The standalone selection suite passed
518 checks against the checked-in frozen source oracle plus native contracts;
this did not regenerate or rerun the historical TypeScript implementation.
