# Committed unit-arrival feedback

Baseline: `aa4ec8a20e9552b7dcef85ec8b66f5ffda1416ce`.

## Observed player experience

Formal C03 T4 brings three friendly units into battle; C10 T6 brings three enemy units; C12 T5 brings two identical enemy ghosts. The original presentation completed their real events and drew the final units, but `unitSpawned` had no text description, journal entry, toast or dedicated camera follow. The generic turn-start banner and unrelated status/resource messages did not identify the arrivals. This is a readability improvement over an explicitly silent presentation boundary, **not a repair to spawning rules or a claim that no visual actors existed**.

The original native journal was inspected through focused keyboard activation after each real AI end-turn. All eight arrival descriptions were empty and the new feedback regression failed across all three levels. Each prefix consists entirely of dispatched legal actions from the unchanged registered level, followed by strict save/load into the real session. The final command is selected and executed by the real default AI, not injected into the event list. These are independent formal-level witnesses, not a completed carried-roster campaign.

The checks use the same `BoardSceneRendering` plane/ground registration and dynamic-state configuration as the game. At the default registered C03 camera and 1280×720, the engineer's center is `(670,753)`, outside the viewport; the other two contacts are at Y=719.25. C10/C12 arrival centers are inside the viewport. This geometry explains why an explicit record is useful, but is **not a GPU screenshot, sprite-occlusion assessment or visual acceptance**. No camera or map is changed.

An earlier exploratory harness incorrectly supplied a generic dimetric plane to C03 instead of its registered plane and hit the depth-range guard. That failed harness/log is retained. It was corrected to consume the existing production composition, with no change to the map or the depth guard; the failure is not classified as a game defect.

## Canonical witnesses

The committed fixtures are `tests/native/c01-{03,10,12}-spawn-feedback.json`.

| Level | Legal sequence | State digest | Ordered events SHA-256 |
| --- | --- | --- | --- |
| c01-03 | 66 + final AI end-turn | `3b9e771a` → `7348bf5f` | `48e9cd994086dfebc9e8ae384be369d4bfc53f9de2b11cfda506ce1db421ce5b` |
| c01-10 | 109 + final AI end-turn | `0a7c8887` → `98eb543f` | `c73c73e5cfe86fededf93d03e10fd6f0c0e918568f7c6e7b43e2b3c866ffe5cb` |
| c01-12 | 89 + final AI end-turn | `221cc712` → `e51dd2d5` | `6e80efeaa513c97e2d59aaf92622ac3739f67ead775978e05af219de5e60879d` |

The three final receipts have no coincident `scenarioSignal`. C10's earlier Ivra signal and C12's later rising-water signal are not substitutes for these arrivals. The formal creation effects use `reason=reinforcement` and `ready=true`, but the generic text deliberately does not classify `reason` or invent a new event category.

## Ownership and implementation

- `ScenarioCommands.SpawnUnits` still owns creation and emits the existing semantic fields: `type`, `unit`, `unitType`, `owner`, `at`, `reason`. No rule, state, action, spawn timing, readiness, position or saved event changes.
- The existing `BattleLogContext.Observe` owns the event-time identity of units created during the current receipt. Text uses this identity and the **event's owner**, rather than assuming the actor survives in the final snapshot or retains its later allegiance.
- The injected content/player catalogs provide localized names. The new PO template is `{0}：{1}进入战场`. No source IDs, raw reason strings or coordinates are shown.
- Arrivals use the application's frozen **after** visibility set, because a new unit is normally absent before creation. Null remains unrestricted, an explicit empty set remains fully hidden, and the renderer/text never recomputes fog. Continuing-unit feedback keeps both-boundary visibility, and departures keep before-only visibility.
- The shared membership helper is renamed `IsVisible` to express its actual use at either boundary. Its null/set behavior is unchanged.
- Delivery remains owned by the existing ordered `EventPresented` path, toast and bounded journal. The two identical C12 ghosts naturally fold to `敌对部队：失名幽魂进入战场 ×2`; no wave-aggregation state is added.
- `unitSpawned` still has no dedicated animation. No recruit/cost semantics, effect, delay, sound, camera jump, map, art or save-format change is introduced. `ACTION-PRESENTATION.md` now distinguishes the textual receipt from the existing no-animation boundary.

This follows the repository's one-fact/one-owner requirement and [Godot's focused scene responsibility and dependency injection guidance](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html). The localized template stays in the established [Godot translation resource pipeline](https://docs.godotengine.org/en/stable/tutorials/i18n/internationalizing_games.html).

## Regression scope

`ReinforcementFeedbackChecks` is part of the normal native gate and currently passes **96 native assertions**:

- C03/C10/C12 exact original command, state and complete ordered-event digest; every event completes once in order, with an authored-owner/type toast only at the arrival slot and no earlier announcement.
- The actual focused native journal, exact per-wave lines and C12 consecutive folding; no new animation, no moved camera and the actual final visible unit projection.
- Settled refresh does not repeat arrivals; strict save/load and rebinding preserve real units/events without replaying historical announcements.
- Explicit synthetic before×after null/visible/hidden matrices, immutable caller-set boundaries, and hidden identity filtering before text lookup. Visible missing identity remains an error, not a fallback name.
- A separately labelled **fog-only C12 variation** uses the same genuine pre-action state and real AI command, changing only `rules.fog`. All ordered events are identical; the final state differs only in fog (`1a7f7470`). Both spawned ghosts are actually hidden by the application's visibility query, neither appears in the native rendered count, and neither is announced or recorded in the journal.

The existing engine-driven transient-creation contract also checks a real supported trigger that creates a unit, transfers its owner, changes its resource, withdraws it and removes its marker within one action. The new arrival line must preserve the creation-time faction and type although neither boundary snapshot contains the actor. Extending this test exposed an object-identity indexing mistake in the test: detached receipt reads do not return the same `JsonObject` instance. The guard now finds the event by semantic type and canonical unit ID, explicitly requires a valid index, and asserts creation precedes its resource event. The original failed test run is retained; the production snapshot-detachment contract is unchanged.

## Verification status

Code checkpoint: `1ac55217ef3ed5de42886cd6744db07147b3d9bb`.

- Targeted arrival regression: **96 native assertions, exit 0**, repeated successfully in the full gate. The unchanged morale regression also passed **74 assertions**.
- Three deliberately broken variants built successfully and were rejected at their intended guards: requiring both visibility boundaries for arrivals (exit 1); announcing hidden arrivals (exit 1); and using the wrong creation-time owner (contract exit 134). The negative runner matched all three expected failures. Original sources were restored before the full gate.
- Objective/outcome contracts: **142 assertions, exit 0**, including the real transient creation/transfer/erasure case and the corrected semantic event-order guard. The earlier test-indexing failure remains recorded separately; it is not counted as a successful run.
- The complete current native gate plus localization foundation and ExportRelease build passed **44 recorded stages**, including **29 project native scenes**. The uninterrupted driver ran **2026-11-04 12:14:37–12:55:47 UTC (41m10s)**; its final completion record was written at 12:55:48. Every stage retained its checkpoint, start and terminal exit 0. There were no interrupted stages.
- Every project native scene and the editor import passed the error/leak/teardown scan. Unlike the preceding morale run, this import did not emit the intermittent editor-settings error. This verifies this run, not a repair to Godot. The supplemental driver checked import diagnostics explicitly; the repository import command itself was not changed.
- Debug and ExportRelease built with zero warnings/errors. Formatting/source selection, **170 script UIDs**, all four authored-map validators and their negative controls passed. Existing authored `runtimeReady=false` diagnostics remain explicit. Warning messages and counts match the preceding gate after normalizing temporary data paths, including the deliberate malformed/unwritable-save diagnostics, missing-art diagnostic fixture, eight duplicate HUD mouse-enter notifications, one camera mouse-exit notification and ignored nested provenance project.
- Core: **13 contracts**, all **16 independent formal levels** and **3,425 legal orders**, with exact strict save/replay round trips. Per-level action counts and state digests match the preceding baseline. These include losses and are not a carried-state campaign victory claim.
- Localization: **46 source tests**, **18 formatter assertions**, **1,226** validated PO messages and exact native translation-resource equality.
- Relevant integrations passed: animation 844; HUD 510; idle guidance 83; dossier focus 73; actual-session BattleScreen; regeneration; scenario announcements 337; morale 74; arrival 96; battle-result flow 371; campaign navigation **8,840 assertions, zero failed groups**; campaign UI 205; lighting lifetime 106; lighting 1,791; scenery 8,753.
- The gate-script and Debug-binary SHA-256 checks matched at completion and during the final audit. The implementation checkpoint stayed clean throughout the aggregate. The subsequent commit only finalizes this verification report.

Headless native input, controls, resources and geometry are **not rendered GUI, physical-device or exported-package acceptance**. No screenshot or visual acceptance claim is made.

```sh
source tools/godot/env.sh
dotnet build godot/Empire.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
"$GODOT_NET" --headless --editor --path godot --import
XDG_DATA_HOME="$(mktemp -d /tmp/empire-native-check-data.XXXXXX)" \
  "$GODOT_NET" --headless --path godot res://Scenes/ReinforcementFeedbackChecks.tscn
bash tools/godot/objective-outcome-checks/run.sh
bash tools/godot/localization-checks/run.sh
EMPIRE_CHECK_TIMEOUT_SECONDS=900 bash tools/godot/verify-native.sh
```
