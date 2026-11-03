# Dossier focus after a committed adjustment — 2026-11-03

## Reproduced operation

In an isolated session using the unchanged C01 opening level, select Torren,
open the unit dossier, expand reactions/facing, and activate Guard with Enter.
The real order commits and advances the battle revision. Close the dossier:
before this repair the current dossier entry does not regain keyboard focus.
The regression failed at that exact focus assertion after confirming the action,
updated revision, settled presentation, and still-valid entry.

The session deliberately returns to its resting selection after dispatch. Its
unit dossier content therefore becomes the current roster content. This repair
does not change that selection policy or retain a stale unit inspection.

## Cause and repair

The deferred close callback required the entire battle stamp to equal the stamp
at opening. That incorrectly treated a successfully committed dossier adjustment
as a stale close operation, even within the same mounted battle binding.

Only this focus-restoration condition changes. It now accepts a nondecreasing
revision within the same binding. It still requires the current HUD generation,
live mounted controls, a valid visible opener, and a hidden dossier. Different
bindings, older revisions, later projections, removed/hidden entries, and unloaded
HUDs cannot receive stale restored focus. The full stamp/generation checks on
opening a dossier and every gameplay intent remain unchanged.

No maps, artwork, resource bindings, rules, localization, save format, or gameplay
transition code changed.

## Regression checks

`DossierFocusChecks` now includes the existing static reading/navigation cases
and two isolated real-session operations. It uses native Enter activation for
Guard and north-facing adjustments, verifies each commits exactly once, and
checks both Close-button and native-hide restoration. Enter on the restored entry
must reopen safely on Close without another order. Closing must not alter the
committed state or append events.

Separate lifecycle cases cover a changed binding, older revision, a projection
arriving after dismissal, a hidden or removed entry, HUD unload, and immediate
reopening through the actual entry's deferred callback. The reopened window must
retain Close focus and native Tab navigation.

The native aggregate script now includes `DossierFocusChecks` beside the general
HUD checks, so this regression is part of its ordinary scene gate.

An exploratory `Hide(); PopupCentered()` in the same synchronous call did not
keep the native popup open in this environment. That unsupported fixture was
replaced with the real entry path; production window behavior was not changed to
make it pass. Native viewport focus is checked in its owning window rather than
assuming background and popup controls cannot both retain focus ownership.

## Verification boundaries

The original implementation was built and run with the new operation regression
before the fix. It failed at the expected post-order close-focus assertion, not
fixture setup. Completed checks on the final code:

- Production build: zero warnings and errors.
- `DossierFocusChecks`: 73 assertions.
- `TacticalHudChecks`: 379 assertions.
- `BattleScreenChecks`: real-session integration passed.
- All three final native processes exited zero with no error, leaked-resource,
  segmentation-fault, or unhandled-exception diagnostics. Existing source-readiness
  and mouse-notification warnings were not suppressed.
- Changed-source whitespace verification, `git diff --check`, and aggregate
  script syntax (`bash -n`): passed.
- Source metadata: 154 unique script UIDs and 14 positive/negative cases passed.
- Independent read-only review: no blockers.

Checks use official .NET 8 and Godot 4.6.3, fresh temporary user-data directories,
and a 600-second process budget. These are headless native control and real-session
checks, not a current GUI screenshot or desktop window-manager Escape acceptance.
The prior [dossier reading report](verification/ui8-dossier-focus/README.md) remains
historical evidence for its original, presentation-only slice.

The repository-wide native formatting gate still has the previously recorded
failures in 184 untouched files. This focused repair does not reformat unrelated
sources or claim that the aggregate native or TypeScript gate is green.
