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

Full aggregate completion results will be recorded after normal stage exits
have been verified.
