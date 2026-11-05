# Deferred HUD orders keep their selection context

## Player failure and native evidence

The unchanged application at `1a0fceedd` was started with fresh isolated user data.
Native keyboard input followed New Campaign, the seven opening story beats,
`steady-advance`, staging and first-battle entry. Space selected Laiya and native
Tab reached her origin Wait button. No focus, button signal or battle state was
injected.

An input batch then delivered Enter, Escape and Space before the deferred callback
ran. Enter activated Laiya's Wait, Escape released native button focus, and Space
selected Torren. The identical Wait row remained mounted. On the next deferred
flush, Torren, not Laiya, had spent his action. The native probe failed its
wrong-actor assertion and exited 1. The same unchanged probe passes after the
repair, leaving both actors unspent and Torren selected.

This is a queued-input ordering defect. A separate frame-separated held-mouse
experiment did not reproduce it: native focus loss canceled that press. An actual
keyboard-driven restart-confirmation route also passed strict shutdown checks and
did not justify any restart implementation change.

## Cause and ownership

Previously a noncanonical HUD intent carried its battle binding/revision and
command key, but not the selection it was meant to act on. Row retention correctly
kept equal controls stable during hover. However, two different actors or
composed destinations could produce identical rows at the same battle revision.
The delayed callback then resolved the old key against the newest selection.

`TacticalSelection` now derives an immutable context value containing its mode,
actor, destination, ability, weapon, commander and passenger. The shared HUD
projection helper includes that value in each noncanonical intent. Existing row
equality retires only controls whose context has changed; the screen also checks
the current context before resolving an intent.

There is no second selection state machine or selection counter. Cursor,
inspection and preview-only updates are excluded. Paths, target subjects and
hostility remain derived from the same stamped canonical queries. Canonical Action
payloads already contain their subjects, and global EndTurn/Undo are unchanged.
No map, artwork, battle rule or save format/meaning was changed.

Godot's [input dispatch documentation](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html)
explains native viewport/control event routing. Its
[deferred-call contract](https://docs.godotengine.org/en/stable/classes/class_object.html#class-object-method-call-deferred)
places deferred work in idle processing. The fix preserves deferred native control
teardown while making the meaning captured before that boundary explicit.

## Regression coverage

The existing natural keyboard campaign route now continues into actual first-battle
interaction. It verifies both actor replacement and same-actor destination
replacement before a pending Wait dispatch. Neither stale order commits. A new
explicit order then acts on the current unit/destination once, and native Undo
restores the exact opening. The campaign checkpoint and manual slot stay unchanged.

Labeled synthetic widget boundaries isolate each context field, including mode,
recruitment site, carrier, attack ability/weapon, tactic commander/identity and
disembark passenger. A hover-only preview update and equivalent derived collections
retain the same row and permit its queued action once. Projection assertions check
that all noncanonical intents carry the supplied selection context. Existing busy,
binding replacement, unmount, touch and deferred-lifetime checks remain enabled.

Focused HUD, campaign-navigation and BattleScreen processes passed strict exit 0
on implementation checkpoint `f131a266a`. A subsequent test-only checkpoint isolates
the synthetic mode case so it changes Kind alone.

A controlled negative removed context stamping and the receiving guard, restoring
the original retargeting behavior while retaining the permanent tests. Its build
passed. The native campaign suite failed exactly one group at the assertion that
Laiya's queued Wait must not spend Torren's action (strict exit 1). Both production
files were then restored byte-for-byte, verified by SHA-256 and a clean worktree.
The full gate rebuilds that restored source before running.

The complete `tools/godot/verify-native.sh` gate passed in one uninterrupted run
on `98b0053d1daaa8185c5f06bb9f06d427c7bdb984`, from 2026-11-05 17:18:39 to
18:14:47 UTC (3368 seconds). This document is the only subsequent source-tree
change.

- Formatting, source metadata, authored scene validators, application/core builds
  and all 16 core groups passed.
- All 16 formal level replays retained the exact ordered action/event/receipt
  traces and final digests across 3425 legal commands.
- All 31 native scenes passed, including the full keyboard, mouse and touch
  routes. All 32 strict process logs, including editor import, were retained.
  Their 40 warning headers exactly match the preceding full gate; none were
  suppressed or reclassified.
- The real played-flow gate again covered normal-AI defeat, a single result view,
  retry and both preserved save slots, followed by C01-01/C01-02 victories carrying
  the same progress into the first 21 legal C01-03 orders. The canonical terminal,
  settlement and third-opening digests were unchanged.
- The separate ExportRelease build passed with zero warnings and zero errors.
  This is compilation, not a newly packaged export.

## Limits

This evidence is native headless input, committed state and lifecycle verification,
not rendered visual or physical-device acceptance. It does not claim that the
whole campaign has been completed. Previously observed sporadic HUD/morale exit
faults remain unresolved; this selection-context repair is not a shutdown fix.
