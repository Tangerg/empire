# Keep modified key chords out of battlefield orders — 2026-11-03

## Reproduction and root cause

The native screen forwarded every pressed key to its bare-key tactical mapping,
without checking Control, Alt, or Meta. In the unchanged C01 opening session,
select Torren and preview a legal destination with Wait available. Control+W was
then interpreted as W: it committed the move-and-wait order instead of leaving
the preview untouched. Modified E and U reached end-turn and undo in the same
way. Even Control+Escape could release a focused native button as bare Escape.

The new regression was built and run against the original adapter. It failed
at `Control+W ... through window input`, using the real application session and
the actual destination-preview path, not a mocked action dispatcher.

## Minimal repair

`BattleScreen._UnhandledKeyInput` now rejects Control-, Alt-, and Meta-modified
key events before inspecting native focus or translating a key into a tactical
command. Both the window-level `_Input` route and the unhandled-key route share
this boundary. Rejected chords are not marked handled, leaving native UI and
platform processing available.

Shift-only behavior, unmodified shortcuts, key-release handling, and autorepeat
policy remain unchanged. The shared tactical selection controller and its source
parity are untouched. This does not add or implement any operating-system
shortcut, and native controls may still interpret their own input normally.

No maps, art, selected resources, rules, localization, or save schemas changed.

## Regression coverage

`BattleScreenChecks.CheckInputContracts` tests 126 modified input cases across
both native adapter entry points:

- A pending destination: W, E, Enter, Escape, Space, Left, A, C, H, S, and D.
- After a real reversible move: U, E, W, Enter, and Space.
- With a native button focused: Escape, Enter, Space, Right, and E.
- Each key is tested with Control, Alt, and Meta separately.

Each case requires the exact canonical state, revision, selection object, HUD
projection, and current native focus to remain unchanged. Existing plain W, undo,
and end-turn tests still execute their real orders. An added Shift+Space case
retains existing actor cycling without advancing the battle. The scene is already
part of the regular native aggregate gate.

These adapter checks call the native input entry points directly. They do not
claim that desktop window managers deliver every chord, or simulate platform
window-closing behavior. Separate native dossier checks cover Tab/Shift+Tab,
Enter activation, close, and reopening.

## Verification boundaries

Completed checks on the final code:

- Production build: zero warnings and errors.
- `BattleScreenChecks`: real-session integration passed, including all 126
  modified input cases and the added Shift-only case.
- `DossierFocusChecks`: 73 assertions.
- `TacticalHudChecks`: 379 assertions.
- All three native scene processes exited zero with no error, leaked-resource,
  segmentation-fault, or unhandled-exception diagnostics. Existing source-readiness
  and mouse-notification warnings remain visible.
- Changed-source whitespace verification and `git diff --check`: passed.
- Source metadata: 154 unique script UIDs and 14 positive/negative cases passed.
- Independent read-only review: no blockers.

All game actions run in isolated fixture sessions with fresh temporary user-data
directories. No player save is loaded or changed. Current GUI/desktop acceptance
remains separate from these headless checks.

The repository-wide native formatting gate retains the previously documented
failures in 184 untouched files. This repair does not change those files or claim
the aggregate native or unrelated TypeScript suites pass.
