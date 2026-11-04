# Committed scenario announcement verification — 2026-11-04

## Scope and ownership

Baseline: `03b4e587dde84c36d23685752a3d3a8bc8387228`.
Implementation checkpoint: `dadc939964cc56b4acda68eb35a96d288372a09c`.

The only production change registers `scenarioSignal` in `BattleEventText` and resolves
its existing label through `BattleText.SignalName(context.State.levelId, event.signal)`.
The scenario system still owns trigger timing and one-shot state. The committed receipt
still owns the event order, the board publishes after presentation, and the screen's
existing log owns retention and toast delivery. There is no second announcement queue,
trigger polling, duplicate localization table, new animation, or new rule.

The original art, maps, units, fonts, scenario data, battle rules and save formats are
unchanged. In particular, White River's T6 signal remains a warning, not a bridge
collapse or terrain/structure mutation.

This follows the Godot guidance on focused responsibilities, injected collaborators,
and signals that respond to completed behavior:
https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html

## Reproduction and player-facing result

The native witness starts from the unchanged C01-06 level, initial state hash
`142efa8b`, with no edits to positions, health, turn number, controllers or scenario
state. It uses native Space/arrow/Enter/W input to retreat Laiya legally and native E
to end each human turn. Every opponent order is generated and committed by the real AI.
The board's production animations execute with a controlled clock. The battle journal
is opened through native focused keyboard activation at the shipped 1280×720 layout.
This is a mounted native headless integration check, not rendered GUI acceptance, and
not a complete carried-state campaign playthrough.

The original presenter failed at both real receipt boundaries:

| Turn | Revision | Signal | Original | Fixed |
| --- | --- | --- | --- | --- |
| 4 | 33 | white_river_pursuit_arrived | Empty description; absent journal entry | 白河追兵抵达 |
| 6 | 61 | white_river_bridge_damaged | Empty description; absent journal entry | 白河桥受损 |

The original reproduction exits 1. The fixed dedicated scene passes 337 assertions
and exits 0 without native teardown errors. It establishes:

- No warning before its committed event, and no T6 preannouncement at T4.
- Every event is published exactly once in order; each warning reaches the actual
  toast at its own publication boundary and appears once in the opened plain-text journal.
- Repeated refresh does not repeat feedback or dispatch an order.
- Loading the genuine T4 save preserves the canonical state and historical event list,
  but binding that loaded session does not replay old warnings or invent new ones.
- Both warning receipts leave the map and structures unchanged.
- Fixed and original native state/event hashes match exactly:

| Boundary | State hash | SHA-256 of complete ordered event list |
| --- | --- | --- |
| T4 | 789b6864 | e395d3f0b5b5f8328b2551cea06bc37d81e0cc92333cd45354f42ccc1b30e45b |
| T6 | 6b0ac5f1 | da1044200ef95d53f6731eb7d8981d3e72e0f78d93c94f6bbf015c4c7b52f59c |

The witness ends at T6. Later one-shot trigger behavior remains covered by the existing
WhiteRiverChecks timing contracts; it is not claimed as additional native gameplay here.

## Signal admission, localization and visibility

Separate, explicitly synthetic text-contract checks enumerate the effect registry's
25 signal declarations across 16 shipped levels. All resolve through their own level's
metadata. Injecting a changed label changes the description; an explicit registry
replacement can suppress it. Undeclared or wrong-level signal identities fail with the
existing localization error. Genuinely unknown event types remain silent.

All current shipped campaign levels are fog-off. Their declared signals are narrative
discoveries, player interactions or global notices, not internal diagnostics or private
actor payloads. Only the authored committed label is displayed; no actor identity,
location or trigger condition is added. The existing actor visibility checks are unchanged.
This does not establish a visibility policy for arbitrary future private signals.

Declared scenario-effect signals are validated against presentation metadata during
composition. An extension emitting undeclared signals directly can fail during receipt
formatting after its canonical action has committed. That error is explicit and does
not imply rollback. The updated feedback contract documents this boundary.

## Negative controls and review

An instrumented text-only positive runner passes 21 assertions. Four deliberately broken presenters each build
successfully but exit 1 at the intended guard:

1. Remove scenarioSignal registration.
2. Display the raw signal identifier.
3. Resolve every signal against the wrong fixed level.
4. Silently swallow a missing signal's localization error.

The full runner and original candidate were restored byte-for-byte before the aggregate.
An independent read-only source/ownership/test review found no blocking defect.

Exploratory failures are retained, not counted as success: doing nothing with the human
army loses the battle before T6; the first standalone journal fixture inherited a 64×64
headless window instead of the supported battle layout; and the first text enumeration
incorrectly assumed every level declares a scenario. The final witness uses legal
retreat orders, the real layout, and the actual optional-scenario contract.

## Aggregate verification

One uninterrupted run on the clean implementation checkpoint completed from
**09:54:55 to 10:34:39 UTC** (39 minutes 44 seconds). All **41 stages** have explicit
exit code 0, including **27 native scenes** and a separate ExportRelease compilation.
The overall process exits 0; no stage was interrupted or accepted from an earlier run.
The gate script and Debug game/core binaries match their before/after SHA-256 records.

- Source selection: 30 tests; active-source formatting; 168 indexed unique script
  UIDs and 4,190 intentionally unimported historical audit files.
- Four authored-map static validators; Debug game/core-test and ExportRelease builds,
  each with zero warnings and zero errors.
- 13 core contract groups, including all 16 independently played shipped levels,
  3,425 legal native actions and exact save/replay round trips. These are independent
  level runs, not a complete carried-state campaign victory.
- Native feedback and controls: scenario signal 337, HUD 510, idle guidance 83,
  dossier focus 73, result flow 371, animation 844 assertions. BattleScreen and
  regeneration suites pass, including hidden-actor recovery suppression.
- Campaign navigation: 8,858 assertions and zero failed groups; campaign UI 205.
- Lighting lifetime 106, lighting/shadow resources 1,791, scenery/resource/lifecycle
  8,753 assertions, plus the full map/camera/input/resource scene cohort.

Existing source-readiness diagnostics and explicitly labeled fixture warnings remain
visible and are not suppressed. Every native process is checked for error, leak,
segmentation-fault and unhandled-exception diagnostics as well as its exit code.
The verification runner now includes ScenarioSignalFeedbackChecks in the normal
aggregate; its scene/script UID are indexed. No historical source-parity suite was
substituted for the current native gate.

No remote CI pass or visual acceptance is inferred from local headless results.
