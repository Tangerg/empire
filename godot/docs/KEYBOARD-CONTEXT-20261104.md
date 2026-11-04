# Keyboard context after leaving the battlefield

## Reproduced player flows

Two native regressions failed with exit 1 before changing production code:

1. Replay the original C01-01 prefix through action 20, move the mouse from real
   ground into the HUD, and physically click the rally button. The normal HUD
   handoff clears the field cursor. Pressing D then replaced the armed Tactic
   selection with Unit and selected an unrelated actor, without executing the
   tactic. The existing recorded-status fixture supplies this original action
   21 and its exact before/after/event hashes.
2. Inspect the shipped C01-03 home at `(38, 23)`, leave the map through the native
   input route, then press D. The mounted Site inspection likewise became Unit.
   Its existing destination was ignored by keyboard-origin resolution.

Both failures occurred while the session revision remained zero: this was loss
of a reversible interaction, not a changed game rule or an unintended cost.

## Root cause and owner

`TacticalSelectionController` owns the current composition. Its direction-key
handler previously considered only a live cursor, a selected actor and incidental
unit inspection. If none existed, it called actor cycling, which replaced the
current composition even when a tactic or site was already selected.

Keyboard origin now uses the composition's existing destination when there is
no cursor or actor. A target-only order first focuses its first canonical
interactive target, preserving target order and never manufacturing `(0, 0)`.
If none is interactive, it consumes the navigation attempt without changing the
order. When the current composition supplies no anchor, the existing incidental
inspection and actor-cycling fallback remains. Existing cursor and actor-origin precedence is unchanged.

The controller returns its existing Reveal/CursorFocused result. BattleScreen
already uses that result to transfer ownership from the old physical pointer
before revealing the keyboard cursor. No new cursor cache, commander coordinate,
scene callback, query port or rule writer was added. Site/Recruit destinations
and tactic targets remain projections of their existing owners.

The change preserves the native focused-control and modifier-chord guards,
pointer hit semantics, busy/revision/binding guards, and explicit Enter
confirmation. It follows Godot's
[input dispatch guidance](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html)
and [scene organization guidance](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
by retaining the owning controller and screen boundaries instead of creating a
second keyboard path in the HUD.

There are no changes to combat rules, engine content, maps, artwork, saves,
translations or production scene resources.

## Regression scope

The existing mandatory TacticalHudChecks scene includes the new partial
`TacticalHudContextChecks.cs` and the site checks. The former uses actual native
mouse motion and button events, then native D/arrow/Escape/Enter events. It checks:

- Real field-to-HUD cursor release and the first keyboard target.
- Cancellation, a second physical button click, window leave and keyboard
  reacquisition without resource spending or session advancement.
- Navigation through the configured grid to the original `(5, 17)` rally target.
- Enter produces exactly the original action, state `d9719d83` and event SHA-256
  `fcb4285b5e83b7038273726014f77c72d7fb35efc3e3dae019f9e6a7f3e77973` from
  `60e996fc`. Selection and presentation settle normally.
- The C01-03 native site remains pinned; a controller-level assertion verifies
  the cursor resumes at `(39, 23)` from its existing site coordinate, without an
  action request. Existing boundary, close and ordinary click checks still run.

The expanded cancellation test initially read a newly rebuilt button rectangle
before native layout had settled. That harness failure is retained separately;
the corrected test awaits layout before clicking and explicitly verifies re-arm
before testing window leave. Neither rules nor assertions were relaxed.

Focused Debug compilation completed with zero warnings/errors. The full
TacticalHudChecks process then exited 0 with **459 assertions** and no strict
error/leak diagnostics. Native input dispatch and retained UI content are tested
headlessly; this is not GPU visual acceptance or an exported-game run. Existing
native pointer-notification warnings and source-authored readiness diagnostics
remain.

The original `tools/godot/verify-native.sh` commands were run as individually
recorded stages against code commit
`adc867a2705f24eb5cabe712585ffe9b9d02792b`, with an additional ExportRelease build.
All **39 stages**, including **25 native scenes**, have confirmed exit 0 between
**00:42:32 and 01:20:54 UTC on 2026-11-04**.

The environment restarted during BattleScreenChecks. The 31 completed stage
results and exact script/game/core binary hashes survived and were verified.
The incomplete BattleScreen attempt was archived without a success result; its
original command reran from the beginning, then the remaining stages completed.
This is a verified segmented recovery, not an uninterrupted full-suite run. The
resumed driver exited 0, final hashes matched, and every completed native stage
passed the strict error/leak scan.

- Source selection, formatting, resource metadata and all four map static
  guards passed without weakening assertions or changing their commands.
- Debug and ExportRelease builds passed with zero warnings/errors. The evaluated
  ExportRelease list has 108 compilation items, including the selection
  controller and excluding both affected HUD test partials.
- All 13 core contracts passed. Sixteen independent formal levels completed
  3,425 legal native actions with exact save/replay round trips. Loss and
  turn-limit outcomes are included; this is not a carried-campaign victory claim.
- Objective/outcome and external catalog ownership passed 141 and 76 assertions.
- Full HUD (459), camera integration (313), animation timeline (844), battle,
  campaign navigation, UI, lighting and scenery regression stages passed.

No live GPU or exported-executable acceptance was performed. Existing native
warnings and authored `runtimeReady=false` diagnostics remain visible.
