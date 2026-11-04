# Committed status feedback

## Player-visible evidence

The formal campaign can apply and end meaningful statuses without describing
those events in its battle journal. For example, C01-14's counterattack applies
armor-down to the sapper, and a later owner turn removes it. The engine emits
both events, but the descriptive presenter previously had neither handler.
Inspecting an individual dossier was the only way to read its current statuses.

A new native regression first failed with exit 1 on C01-01's actual rally button
and ground-target selection: three committed inspiration applications had three
empty descriptions. These are original, legal action prefixes, not injected
status changes. The fixture records zero-based action indices:

| Level / action | Operation | Before | After |
| --- | --- | --- | --- |
| C01-01 / 21 | First rally application | `60e996fc` | `d9719d83` |
| C01-14 / 56 | Counterattack applies armor-down | `54eeecaa` | `edc3da4b` |
| C01-14 / 129 | Real AI turn ends; status expiry | `630b1134` | `7212129c` |
| C01-03 / 45 | Rally refreshes existing inspiration | `77cf8882` | `aeefea4d` |

`tests/native/recorded-status-feedback.json` preserves the original level initial
hashes, exact action prefixes and ordered-event SHA-256 values. They were checked
against the existing formal histories, independently of the presenter. Native
checks replay each prefix with the current engine, execute its checkpoint through
the real application session, and require the exact original action, final hash
and event hash. There is no manual relocation, health adjustment or added status
in these campaign witnesses.

## Ownership and scope

- `StatusTransitions` remains the sole status-application/removal writer.
  Application receipts already carry the resulting duration and stack count.
  `StatusLifecycle` still owns owner-turn expiry.
- `BattleEventText` describes those committed receipts through its existing
  registry. Application wording says the status, remaining turns and stacks;
  it does not claim every application is new or add duration a second time.
  Removal wording states that the effect ended without inventing a cause.
- The formatter receives `ContentText` explicitly from
  `SessionPresentationText`. Status names come from the composed catalog and
  localization, never an ambient/default catalog or hardcoded ability list.
- Both new handlers use the existing frozen before/after visibility boundary
  **before** resolving unit or status identity. Hidden units do not gain new
  journal disclosures.
- Existing `BattleScreen` receipt handling owns the toast and bounded journal.
  There is no new animation, signal connection, timer or status cache. Each
  event keeps its original index; an empty concealed description keeps its slot.
  Later receipts can replace a toast, while the journal retains recent lines.

No engine rules, status durations, combat calculations, maps, artwork, save
format or navigation semantics changed. Godot's
[scene organization guidance](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
is followed through explicit composition and the existing owning screen rather
than discovering another service or duplicating state in the view.

## Regression boundaries

`BattleScreenStatusChecks.cs` joins the existing mandatory `BattleScreenChecks`
scene and is excluded from ExportRelease by the existing `*Checks.cs` boundary.
It mounts the authored scenes through their actual `BoardSceneRendering` plane
and ground factories, invokes available native order controls, reads the actual
journal dialog, and checks all event indices complete once in order. Repeated
Refresh calls cannot duplicate settled receipts.

Additional isolated formatter negatives cover all null/empty/visible
before-and-after visibility combinations for application and removal; hidden
unknown identities; frozen visibility after caller mutation; a receipt whose
status is absent from the final projection; and two independent localization
compositions. These detached boundary checks are not presented as played
campaign actions.

Animations are disabled for these synchronous receipt assertions. They test
native control callbacks and retained dialog content, not physical pointer
input, GPU rendering, visual legibility or an exported executable.

## Verification record

The original missing-description native RED returned exit 1. The first expanded
run then rejected an incorrectly bound test fixture: C01-03 cannot use the
fallback dimetric plane, and a headless root needs an explicit window size before
opening a journal. The fixture now uses production scene rendering and restores
its temporary window size. Neither the production depth guard nor the maps were
changed to make this pass. These failed runs remain evidence, not passed gates.

The focused Debug build passed with zero warnings/errors. The complete native
BattleScreenChecks process then exited 0, including all four formal status
checkpoints and the existing integration regressions, with no strict error or
leak diagnostics. The existing intentional missing-art warning-path probe and
authored `runtimeReady=false` diagnostics remain. Format write and source
metadata checks also exited 0.

The original `tools/godot/verify-native.sh` commands ran as individually recorded
stages against code commit `65c9b8b80d84af4e764f96ebd737dbce24c48d88`, with an
additional ExportRelease build. All **39 stages**, including **25 native scenes**,
completed normally with exit 0 from **2026-11-03 23:50:45 UTC** to
**2026-11-04 00:23:53 UTC**. This run had no interrupted stage or restart. The
original gate script and Debug game/core binary hashes were verified unchanged
at completion; every native stage passed the strict error/leak scan.

- Source selection, formatting, resource metadata and four authored-map static
  guards passed without exclusions or relaxed assertions.
- Debug and ExportRelease built with zero warnings/errors. MSBuild's evaluated
  ExportRelease list has 108 compilation items: both changed production C# files
  are included; the BattleScreen test partials are excluded.
- All 13 core contracts passed. The 16 independent formal levels executed
  **3,425 legal native actions** with exact save/replay round trips. Some outcomes
  are losses or turn limits; this is not a carried-state campaign victory claim.
- The objective/outcome and external catalog-ownership checks passed 141 and 76
  assertions respectively.
- BattleScreen's four exact status witnesses, the 437-assertion tactical HUD,
  313-assertion camera integration, 844-assertion animation timeline and remaining
  battle, campaign, lighting and scenery suites passed.
- The affected historical feedback helper also compiled separately with zero
  warnings/errors, using already-restored dependencies and without rebuilding
  the gated binaries. Its legacy TypeScript oracle generator was not run, and
  this does not claim a new historical source-oracle comparison.

The full gate remains headless native/resource verification. Live GPU visual
acceptance and execution of an exported game were not performed. Source-authored
`runtimeReady=false` diagnostics are still present and are not waived by these
results.
