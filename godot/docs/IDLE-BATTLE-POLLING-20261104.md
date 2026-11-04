# Idle battle polling: revision-owned status projection

## Observed cost

Baseline: `4fa6adbf888d21a4a932358b812356b970a79923`.

A native application probe follows New Campaign, the actual opening story and choice, and Start Battle. It measures the existing `GrayFlagGame._Process` callback in the settled first battle, first at the opening and then after a legal player move. The mounted application is otherwise paused during each sample so that no asynchronous gameplay or rendering work is attributed to this callback. Each sample warms 32 calls and measures 600 calls; there are three samples per state.

- Opening application polling: 470,644,800 managed bytes per 600 calls, excluding the probe's 40-byte timer allocation. Observed times: 435.5–507.4 ms.
- After a legal move: 482,232,000 managed bytes per 600 calls, excluding that timer. Observed times: 406.6–443.0 ms.
- The battle screen's own idle `_Process` measured no managed allocation beyond the same 40-byte timer.

These are CPU-side callback measurements on the available headless .NET/Godot runtime, not total frame time, GPU measurements, an FPS claim, or physical-device acceptance. At a hypothetical 60 calls/second, the baseline application polling alone would request about 47–48 MB/second of managed allocations without changing the battle.

## Cause and ownership

`GrayFlagGame.ProcessBattle` asks `GrayFlagSession.IsComputerTurn` each frame and then reads the phase from `session.Snapshot()`. Both paths deep-cloned the complete private battle, including its map and units, even when no command had changed it.

`Battle` remains the only mutable battle-state owner. `GrayFlagSession` now caches a small immutable status projection containing the existing session revision, whether the current active turn is computer-controlled, and whether the battle is concluded. It derives the projection from a detached snapshot only when the revision changes. Successful Commit and Undo already advance that revision; rejected orders do not. Load and restart create independent sessions with an initially absent projection.

The application polls `IsConcluded` rather than cloning a battle to inspect its phase. `CanUndo` retains its live undo-stack admission and uses the same status projection. The cache contains no mutable state, cannot issue transitions, is not serialized, and does not alter the core engine, rules, save schema, action history, scenes, or artwork. The pacing code still reads status normally after commands, and animation settlement remains the gate for result presentation.

This follows Godot's guidance to [measure before optimizing](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html) and [repeat CPU-side measurements](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html), while retaining [parent-owned scene coordination](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html).

## Regression evidence

`CampaignNavigationPollingChecks` exercises a real opening, a legal move, Undo, and native F5/F9 restoration. It asserts unchanged battle digests/revisions and uses a warmed managed-allocation budget rather than a hardware-dependent timing assertion.

- Original production code fails the new allocation guard: 470,644,800 bytes in the opening sample.
- Fixed production code measures 153,600 bytes per 600 calls in all four states, across all twelve samples. This retains the existing 256 bytes/call for callback/delegate construction and removes roughly 99.97% of the measured allocations. This bounded fix does not add another lifecycle mechanism merely to eliminate those small remaining allocations.
- The 256 KiB regression budget permits those measured delegates but is below one full first-battle clone. Deliberately restoring only the old phase snapshot path fails with 235,296,000 bytes.
- Deliberately removing the revision-key comparison fails the existing real end-turn input assertion at the first human-to-AI handoff. Both mutations are restored before final validation.
- The existing legally played victory driver now compares both status properties to detached canonical state after every order, including AI/human handoffs and the terminal order. Existing navigation coverage retains restart, camp, failed/stale commands, real AI presentation, and result settlement checks.

The targeted GREEN run passes 94 assertions in its two selected groups. It is not the complete navigation suite; the full runner retains every original group plus the new polling group.

## Final verification

Implementation checkpoint: `e33847e649c13054e2f134bce68cae46d602e58d`.

All 39 stages of the current `tools/godot/verify-native.sh` commands plus an ExportRelease build completed with exit 0 on 2026-11-04 at 03:28:26 UTC. This was **segmented validation after an environment restart**, not a single uninterrupted run: the first nine completed stages retained their recorded exit codes, the unfinished core-replay log was archived, and that original command plus the remaining stages were then run to completion. No interrupted result was counted as passed.

- Source-selection negative tests, formatting, source metadata (164 unique script UIDs), and all four authored-scene static validators passed.
- Debug, core-test, and ExportRelease builds completed with zero warnings/errors.
- All 13 core contract groups passed. All 16 shipped independent levels completed 3,425 legal native orders with exact save/replay round trips; this is not a carried-campaign victory claim.
- Objective/outcome (141 assertions) and external catalog ownership (76 assertions) passed.
- All 25 native scenes exited 0 and passed strict shutdown/error/leak checks. Animation (844), HUD (459), dossier focus (73), result flow (371), complete campaign navigation (8,578), and lighting lifetime (106) passed.
- The full navigation run repeated all twelve allocation samples at exactly 153,600 bytes per 600 calls, including the real save/load flow. Its legal-order status comparisons and played-victory continuation passed. Sample times were 0.114–0.407 ms in this run, reported as callback timings only.
- The aggregate script and final Debug binaries matched their recorded hashes. The implementation worktree was clean at completion. The final verification update changes only this report.

Evidence is retained in `/tmp/empire-idle-polling-20261104`, including original baseline measurements, RED/GREEN and mutation exits, per-stage records, the archived interrupted core log, and final stage results.

No new normal-play storage or campaign-navigation defect was established by this batch's independent read-only audit. An externally altered roster-content candidate remains unverified and is not included in this change. Manual GUI, physical-device performance, and exported executable acceptance remain separate.
