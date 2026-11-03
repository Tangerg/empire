# One turn handoff per fresh E press — 2026-11-03

Implementation update: the [contextual repeat repair](UI-CONTEXTUAL-KEY-REPEAT-20261103.md)
moves repeat policy into the tactical controller while preserving this behavior.
The native-mapping implementation below records the original repair.

## Confirmed failure

The native campaign navigation suite opened the unchanged C01 battle through
Continue and Start Battle. One fresh E submitted exactly one `endTurn`. Normal
`GrayFlagGame._Process` calls then ran the actual AI, its animations and pacing
until the next human turn was playable.

On the original implementation this reached turn 2, revision 10. The next
`InputEventKey` with `Echo: true`, sent through the root viewport, immediately
submitted another handoff and reached revision 11. The negative baseline failed
at `Held E after AI must preserve the next human turn through viewport: revision
11, expected 10`. The other fourteen navigation groups passed.

The busy gate is correct during computer playback, but it cannot distinguish a
fresh press from an operating-system repeat after control returns to the player.
An accepted turn handoff also clears undo history; this is not a harmless preview.

## Repair

The native key mapping now accepts E only when `key.Echo` is false, as it already
does for U. This is the boundary that owns native key-repeat metadata. Both the
window `_Input` path and `_UnhandledKeyInput` use that mapping.

No timer, cooldown, held-key latch or new game state is introduced. Release
followed by a fresh press still hands off the next turn. The End Turn button,
shared tactical controller, AI pacer, and rules keep their existing contracts.
Arrow/WASD repeats and modifier filtering are unchanged. No map, artwork,
save schema, manual-save entry point or campaign semantics change.

## Regression coverage

`CampaignNavigationChecks.CheckEndTurnKeyRepeat` is part of the ordinary native
navigation suite. It uses an isolated save directory and the real campaign,
session, screen, input routing, process loop and default automatic animations.
It does not directly advance the AI, synthesize a return-to-human state, or
turn off presentation.

It checks:

- A fresh E dispatches exactly once; another fresh press while busy is ignored.
- Repeated E cannot dispatch during the computer cycle. The final test explicitly
  observes computer animation and the settled human handoff while the application
  pacer is still busy.
- After all busy gates clear, repeated events through the root viewport, window
  input and unhandled-key input preserve the exact state, revision, action/event
  records, HUD projection and visible log.
- Release does nothing; the next fresh E starts and completes a second actual AI
  cycle, so the shortcut has not become permanently locked.
- Neither the campaign autosave nor the separate manual save is changed.

The broader battle-screen suite retains the existing U-repeat, held-arrow/WASD,
modifier-chord, button, busy-state and real-order checks. These are headless
native input-event tests, not desktop keyboard-hold or window-manager acceptance.

## Verification boundaries

Completed verification:

- Production build: zero warnings and errors.
- Campaign navigation: all 15 groups passed, including both actual AI cycles and
  the stricter computer-animation/post-settlement observations (7,046 assertions
  in this run; frame-sampling assertions vary with runtime).
- `BattleScreenChecks`: complete real-session integration passed.
- `DossierFocusChecks`: 73 assertions passed.
- `TacticalHudChecks`: 379 assertions passed.
- All four native processes exited zero without error, leaked-resource,
  segmentation-fault or unhandled-exception diagnostics. Existing expected
  storage-failure, source-readiness and mouse-notification warnings remain visible.
- Changed-source whitespace verification and `git diff --check`: passed.
- Source metadata: 154 unique script UIDs and 14 positive/negative cases passed.
- Independent read-only review: no remaining blockers.
- Accepted C07 scene and selected assembly hashes are unchanged.

The pre-repair failure is negative evidence, not an accepted run. The full native
format gate has the existing failures in 184 untouched files; neither changed
source is among them. This focused fix does not claim a complete native aggregate,
TypeScript suite, graphical session or exported-package pass.
