# Gravekeeper hit full native gate: passed

The unchanged original `tools/godot/verify-native.sh` completed one full aggregate from clean tested source `27de886c111600c64a868f9d57fc6868a6db45e4`. It started at `2026-11-11T00:18:25Z`, finished at `2026-11-11T00:53:23Z`, returned strict exit 0 and ended with the original terminal success marker. It was not canceled, restarted, split or reconstructed from partial runs.

The complete 228,615-byte log has SHA256 `66704d2e898cc84517869df4c50cac801a3af2dea1bdfc83c7855c64bafa9c4c`. Archive-only and actual frozen/live-DLL comparisons pass. All 26 comparator controls pass after completion. Raw execution records are preserved byte-for-byte; `archive-provenance.json` records their copy hashes.

## Immediate baseline and stage provenance

The immediate accepted behavioral baseline is the **gravekeeper idle aggregate**, tested source `869078976dd21f5d82932dc4151bdd03fa973c1a`, with exact full-log SHA256 `70bf3ca37b793e52fb2f42b5f87b5010a73d316448e0c6982cf44fc648ebe410`. Its source, strict exit, terminal marker and successful archive comparison verify.

The current 16 ordered AI traces, containing exactly 3,425 independent legal orders, match that idle log directly, including every action/event/receipt digest. All six played-flow outcome markers and 14 progress/roster markers also match exactly. The normalized multiset of 41 warnings is unchanged. There are no new error or leak diagnostics.

The historical archer-hit 45-stage staged recovery supplies **only coverage provenance**. Its log, plan, completion/source records, runner exit and all 45 stage result/log hashes verify. It is never treated as one single-process aggregate or as the immediate behavioral baseline. All 45 ordered stage markers are present in both accepted idle and current hit aggregates. Silent-stage completion requires successful termination of the unchanged original gate.

## Frozen build identity

Tested Godot tree: `02b18b80aff45880bb58e503574b858c921449aa`.

Tested tools tree: `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`.

The pinned gate-script SHA256 is `4ca078f295e2b3159c0d533afe94f5a5bae146d91a325c4872658166e7e2ab83`. The gate and played-flow sources are byte-identical between accepted idle and hit tested sources. Later documentation-only commits preserve the exact tested Godot/tools trees and do not replace the tested implementation commit.

Actual frozen and live DLL bytes match the original native-recording build:

- Empire.Core.dll: `ab6a38e4b863a6a09acce6505aac93a4fe72975f0c9e41329d1b8645a7c56faa`
- Empire.dll: `9c26ba406a55703a0d5a0b07141cfc094ece56b0842157ec47a03d465f0ec9c0`
- GodotSharp.dll: `2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba`

The frozen hash record retains its original historical absolute paths; the portable comparator uses file basenames. Four build summaries report zero warnings and zero errors. Metadata reports exactly 215 unique script UIDs.

## Assertion totals are not scenario counts

The hit aggregate reports 7,487 animation assertions and 22,257 played-flow assertions. Played-flow reports two carried victories across 415 legal orders and a 21-order third-battle opening, not a third-battle victory or complete-campaign run.

Played-flow's total is 137 lower than accepted idle's 22,394. `CampaignPlayedFlowChecks.cs`, `CampaignPlayedCarryChecks.cs` and the original gate script are byte-identical between tested sources. `Require` increments the count on every call. The defeat wait at lines 121–126 and the carried `Until` helper at lines 382–389 each call `Require` once per awaited `ProcessFrame`. The original gate uses normal automatic playback without a fixed simulation frame rate. The total therefore includes variable polling observations, rather than counting distinct semantic scenarios.

Every played outcome, order count, state digest and progress/roster marker remains exact. No relevant test was deleted or weakened. The logs lack individual polling-loop counters, so the precise 137-count difference cannot be assigned among loops or used as performance evidence. Navigation likewise reports 9,767 versus idle's 9,817 assertions, with unchanged sources and zero failed groups. `assertion-count-review.json` records the source blob IDs, SHA256 values and evidence. Animation's increase from 6,876 reflects intentional new recoil checks.

## Comparator and controls

`compare-gate.py` is portable and read-only. It launches no Godot process, build, import or game test. It separately fixes the current metadata count at 215 and preserves exact AI trace order/counts/digests, semantic played markers and warning multiplicities. Stage-marker normalization changes only numeric assertion totals and UID counts. Warning-path normalization is restricted to the original gate's isolated temporary save roots.

A missing final exit remains pending (return 2). A completed nonzero exit, missing terminal/stage, or evidence mismatch fails (return 1). Missing or malformed required completed hash records error rather than passing. Return 0 means all requested checks passed. No partial log or synthetic fixture constitutes gate acceptance.

All 26 isolated controls pass, covering trace/digest/order changes, played outcomes, progress, pending/completed evidence, source/assembly identity, unpinned/dirty source, missing/duplicate markers, warning multiplicity, errors/leaks, build warnings and nonsemantic assertion-total normalization. Each mutation helper asserts its fixture actually changes. The controls deliberately use synthetic temporary build identities and never establish runtime success.

From the repository root:

    python docs/art-gravekeeper-hit-20261111/verification/full-native/compare-gate.py --output /tmp/keeper-hit-comparison.json
    python docs/art-gravekeeper-hit-20261111/verification/full-native/test-compare-gate.py -v

For evidence outside the repository, pass `--reference-root /path/to/empire/repo`. No machine-specific path is embedded in either script. `--log`, `--exit`, `--source`, `--source-status` and `--hashes` select external evidence. `--baseline` changes only historical staged coverage provenance, not the pinned immediate idle baseline. Add `--frozen-dir` and `--runtime-dir` to verify actual DLL bytes.

This aggregate establishes headless contracts and the played opening. Native visual/evidence review is recorded separately in `../native/`. Complete-campaign, standalone export and realtime-performance acceptance remain outside this slice.
