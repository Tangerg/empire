# AI agenda read projection — 2026-11-04

## Measured scope

Published baseline: `7cc57db5e3f56b542ba7ad4bd19c589a5e2a0684`. The application synchronously asks the canonical AI to choose an action during battle processing. An exploratory cold/mixed probe suggested a cost worth isolating; those exploratory numbers are not the accepted before/after comparison.

The controlled experiment builds three isolated copies with the same .NET 8.0.427 Release settings: unchanged baseline, only `AiAgenda.Read` switched to the existing immutable catalog projection, and that change plus the separate objective-advisor site scan. Six genuine enemy-turn checkpoints in C01/C03/C07 are rebuilt from unchanged formal levels and legally dispatched command prefixes. All six selected orders are early-turn move/wait decisions. These are independent-level witnesses, not a carried full-campaign playthrough, and their timing is not extrapolated to combat/recruitment decisions or every turn. Each variant uses the same inputs, four warmups and eleven measured samples per checkpoint/query, then a second round reverses both variant and checkpoint order. Tiered JIT is disabled consistently. All measured answers, input-state digests and immutable catalogs are checked. Measurement is `Stopwatch` per query and current-thread allocated bytes, outside validation/serialization work.

The candidate adopted below is the ordinary agenda scan only. Across 22 samples per checkpoint, complete decision medians were:

| Formal checkpoint | Complete decision, ms | Allocated bytes/decision |
| --- | ---: | ---: |
| c01-01-step-0 | 139.97 → 130.44 | 25,983,032 → 22,554,576 |
| c01-01-step-5 | 123.95 → 114.59 | 27,649,764 → 24,221,308 |
| c01-03-step-1 | 139.90 → 97.33 | 37,602,784 → 21,663,408 |
| c01-03-step-5 | 112.52 → 79.87 | 35,464,624 → 19,525,248 |
| c01-07-step-0 | 201.48 → 159.65 | 55,493,640 → 35,715,880 |
| c01-07-step-5 | 154.27 → 114.26 | 49,826,364 → 30,048,604 |

The scan itself drops from roughly 3.77/17.54/21.65 MB to 0.34/1.60/1.88 MB for C01/C03/C07. Complete-decision allocation falls about 12–45%, and the measured decision median falls about 7–30% on this environment. The extra advisor-scan candidate showed identical allocation to the ordinary-scan candidate on these real witnesses, so that extra change was **not** adopted. No FPS, GPU, rendered-frame, device-wide or whole-turn speedup is claimed. Individual samples and build/source hashes are retained with the experiment; the committed opt-in `tools/godot/ai-agenda-benchmark` tool reproduces the measurement against an explicitly supplied assembly.

This uses Godot's [measuring and hypothesis-testing guidance](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html) and [CPU timing guidance](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html): control inputs/configuration, account for warmup/order effects, repeat timing and distinguish a CPU query from rendered performance.

## Ownership and behavior

`StateContent` already owns normalized catalog facts and an immutable `JsonElement` projection. The old scan requested a detached mutable `JsonObject` for every tile even though it never wrote to any definition. The change consumes the existing immutable representation; it adds no cache, mutable view, content owner or cross-engine state.

Tile order, owner filtering, HQ and production weights, objective-advisor boundaries, tie order and floating-point scoring remain unchanged. Optional `capturable`/`hq` fields still treat missing, null and non-booleans as false. The `produces` array is still inspected only for a capturable non-owned non-HQ target; malformed data keeps the exact existing `DomainInvariantException`. Unknown terrain IDs and malformed owners retain their old failure boundaries. The separate `Sites` advisor intentionally remains unchanged, including its distinct HQ-only behavior.

The new core regression covers those boundaries, mixed own/allied/enemy/neutral targets, ordered advisor output, same-ID independent catalogs, caller mutation isolation and state/save invariants. A broad allocation budget based on measured current behavior rejects full-definition cloning without imposing machine-speed timing assertions. It reconstructs all six formal checkpoints and checks the frozen action and repeat-read behavior. No map, artwork, rule constants, content, localization or save schema was changed.

## Verification checkpoint

- The baseline passes the new semantic boundary fixtures, then fails the allocation guard at 3,772,980 bytes for C01's 1,080 tiles, as intended.
- The candidate passes the same boundary fixtures and all six action/state/save witnesses. Debug measurements are approximately 344,524 / 1,601,772 / 1,875,428 bytes per agenda for C01/C03/C07, safely within the regression budget.
- The isolated strict-property mutant builds successfully and fails the boundary fixture with the expected `KeyNotFoundException` (test exit 1), proving that missing boolean flags cannot silently become required fields.
- `ReplayChecks` now reports a SHA256 over every ordered action, event sequence and committed receipt fact. The unchanged baseline engine is being run with this same harness so the full candidate gate can compare exact traces for all sixteen shipped levels, not merely action counts or final-state digests.

Full trace parity, the complete canonical gate, final build/format checks and publication remain pending at this checkpoint. Headless native validation is not GUI/physical-device acceptance.
