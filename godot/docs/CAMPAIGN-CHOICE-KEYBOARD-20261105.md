# Keyboard entry into campaign choices

## Reproduced player failure

On `c1e6ddb85`, a fresh application was started with isolated user data and its
normal initial menu focus. Real Enter input opened New Campaign and advanced all
seven original opening story beats. No test code assigned focus or emitted a
button signal. The first authored choice then had no focus owner. Six forward
Tabs and six reverse Tabs all left it empty: a keyboard-only player could not
reach either option. The campaign save remained unchanged and the strict native
probe exited 1 at the reachability assertion.

The surrounding title, story and staging views already seed focus through
`CampaignUiStyle.InitialFocus`. `StoryScreen.ShowChoice` omitted that composition.
Previous choice tests used explicit activation, or real mouse/touch, so they did
not establish natural keyboard arrival.

## Ownership and repair

The choice view now passes its first rendered, available option to the existing
`InitialFocus` helper. That helper already owns deferred visibility/processability
checks, cancellation when a target leaves the tree, and preservation of an
existing focus owner. No new focus manager, keyboard interception, auto-selection
or alternate campaign transition is introduced. The production change is two
lines in `StoryScreen.cs`; rules, map/art, option order and save semantics are
unchanged.

Godot's [keyboard navigation guidance](https://docs.godotengine.org/en/stable/tutorials/ui/gui_navigation.html)
requires an initial focused control and demonstrates deferred focus assignment.
The existing helper composes that native lifecycle rule without stealing a newer
explicit selection.

## Verification

The unchanged external probe passes after the fix: the first option receives
focus, both Tab directions visit the actual options, and traversal alone does
not change the autosave. The baseline failure and passing candidate logs are
both retained.

The narrative checks cover all 11 authored choice presentations without selecting
an option merely by focusing it. Additional checks cover immediate rebind,
explicit focus chosen before the deferred callback, and an omitted first authored
option: focus follows the first actually available option. Focused narrative
checks passed 3323 assertions across 35 documents and 91 beats; the existing
campaign UI flow passed 209 assertions, both with clean strict exits.

The existing native campaign route now also runs a keyboard mode. It uses only
native Enter, Tab, Shift+Tab and Escape, with no `GrabFocus` or signal shortcut.
It covers first story arrival, alternate-option navigation without save changes,
exit while still unchosen, Continue back to that exact choice, explicit selection
with canonical consequences, staging exit/Continue and first battle entry without
an order. Existing quick-touch, split-touch and mouse routes remain enabled.

A controlled negative removed only the two production focus lines while retaining
the new checks. Its build passed, and the strict narrative process failed at the
authored-choice focus and retired-option rebind assertions (exit 1). The source
was then restored byte-for-byte, verified by SHA-256 and a clean worktree. The
full gate rebuilds the restored source before evaluating it.

The full `tools/godot/verify-native.sh` gate passed in one uninterrupted run on
source commit `fe8c84ca2b30d481dd67b9e41de4902e9cfbbfbe`, from 2026-11-05
15:34:06 to 16:26:04 UTC (3118 seconds). This document is the only later change.

- Format, source metadata, scene validators, both builds and all 16 core groups
  passed. All 16 formal level replays preserved the exact ordered action/event/
  receipt traces and final digests across 3425 legal commands.
- All 31 native scenes passed strict process and shutdown checks. All 32 raw
  process logs, including editor import, were retained. The 40 warning headers
  exactly match the previous full gate; they were not suppressed or reclassified.
- The campaign navigation suite passed 9698 assertions and all four natural-input
  routes. Keyboard selection of the alternate option entered the canonical initial
  battle `9dae091b`; the mouse and touch option remained `78e7c7b9`.
- The played-flow suite passed 21668 assertions: a normal-AI defeat, one result
  presentation, correct retry and unchanged slots; then actual C01-01 and C01-02
  victories carrying progress into the first 21 legal orders of C01-03. Its known
  terminal, campaign and third-opening digests were unchanged. This is not a claim
  of third-battle victory or completion of the full campaign.
- A separate ExportRelease build passed with zero warnings and zero errors.
  This is compilation, not a newly packaged or visually accepted export.

## Separate shutdown investigation and limits

Before this repair, a predeclared alternating cohort ran three unchanged HUD and
three unchanged morale processes on `c1e6ddb85`, each with fresh isolated data.
All six passed strict exit/diagnostic checks, and the application/core Debug DLL
hashes were unchanged throughout. The old intermittent HUD SIGSEGV and morale
signal-disconnect diagnostics did not reproduce. Some historical HUD survivors
match test-owned input wrappers, but no causal crash explanation was established.
No resource disposal, garbage-collection behavior or log filter was changed.
These shutdown observations remain unresolved, not repaired by focus admission.

This is native headless input, state and lifecycle evidence. It does not establish
rendered visual quality, screen-reader/device acceptance, a new packaged export
or completion of the whole campaign.
