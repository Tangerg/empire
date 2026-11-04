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

## Verification

- The baseline passes the new semantic boundary fixtures, then fails the allocation guard at 3,772,980 bytes for C01's 1,080 tiles, as intended.
- The candidate passes the same boundary fixtures and all six action/state/save witnesses. Debug measurements are approximately 344,524 / 1,601,772 / 1,875,428 bytes per agenda for C01/C03/C07, safely within the regression budget.
- The isolated strict-property mutant builds successfully and fails the boundary fixture with the expected `KeyNotFoundException` (test exit 1), proving that missing boolean flags cannot silently become required fields.
- `ReplayChecks` now reports a SHA256 over every ordered action, event sequence and committed receipt fact. The unchanged baseline engine was built from an isolated archive whose 155 Core source files were verified against the published baseline, then run with the same trace harness. All sixteen candidate traces match the baseline exactly: **3,425 legal actions**, their ordered events and receipt facts, every final-state digest, and each save/replay round trip. These are independent full-level runs, not a carried-state campaign victory.
- The committed benchmark runner was separately built against an explicitly supplied candidate assembly. All twelve agenda/decision answers match the controlled experiment. Timing from that extra runner check is not included in the table because a separate replay process was running concurrently.
- The actual canonical command `EMPIRE_CHECK_TIMEOUT_SECONDS=900 EMPIRE_REPLAY_TIMEOUT_SECONDS=1200 bash tools/godot/verify-native.sh` ran once, uninterrupted, from **15:52:28 to 16:47:04 UTC** on implementation commit `11c097fb409c3947a91c81edab59b34134fc5b6c`, and exited **0** after 54 minutes 36 seconds. It includes source selection/format/metadata guards, all four map validators with negative cases, both Debug builds, **14 core groups**, objective/catalog boundaries, editor import and **30 native scene processes**.
- All **31 raw native/import logs** were retained and independently checked against the same strict diagnostic boundary: no `ERROR:`, leak, segmentation fault or unhandled exception. The 30 warning headers, after temporary save-path normalization, exactly match the previous successful full gate. These include deliberate corrupt-save/storage-error and missing-art fixtures, existing synthetic pointer warnings and the preserved nested provenance-project warning; no new warning was ignored.
- Native integration includes HUD (510 assertions), camera (313), animation (844), campaign navigation (8,790; zero failed groups), staging opening (160), resource lifetime (106), lighting (1,791) and scenery (8,753). These numbers describe their respective contracts, not visual acceptance. The native staging fixture completes teardown without the prior host shutdown/leak failure.
- The final `dotnet build godot/Empire.csproj -c ExportRelease --disable-build-servers -m:1 -p:UseSharedCompilation=false` exits **0**, with zero warnings and zero errors. No production or test source changed after the canonical gate; this final documentation update records its results.

No data migration is required. This is a measured CPU/allocation improvement with unchanged battle behavior on the complete shipped-level comparison. Headless native validation and an ExportRelease assembly build are not GUI, physical-device, packaged-export or FPS acceptance.
