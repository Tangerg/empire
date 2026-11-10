# Native gate comparison: passed after full repeat

The unchanged original `tools/godot/verify-native.sh` completed in one full
repeat from clean detached source `869078976dd21f5d82932dc4151bdd03fa973c1a`.
The actual retry started at `2026-11-10T23:13:09Z`, finished at
`2026-11-10T23:47:52Z`, returned exit 0, and ends with the original aggregate
terminal success marker. This directory's completed log, exit, finish, start,
source, empty clean-status, frozen-hash and build-comparison records are exact
copies of `QA/verification-retry`, not the earlier partial attempt.

The 227,365-byte full log has SHA256
`70bf3ca37b793e52fb2f42b5f87b5010a73d316448e0c6982cf44fc648ebe410`.
`archive-provenance.json` records each original/archive byte comparison and hash.
`comparison-archive-only.json` and `comparison-live-dlls.json` both pass.

## The interrupted first attempt remains separate

The first attempt began at `2026-11-10T22:44:17Z` from the same clean tested
source. Its process was lost when the execution environment changed. The
implementation parent verified no surviving gate/native/Godot process and
received `Unknown process id` for the old session. Its exact 93,327-byte log,
source/start/clean-status records and interruption report are preserved in
`interrupted-attempt/`. That log ends after the 402-assertion battle-result-flow
marker. There is no exit, finish record or aggregate terminal marker for that
attempt, and none has been inferred or fabricated. It is not an aggregate pass.

The successful repeat reran the complete unchanged gate, not just remaining
stages. The repository was temporarily detached at the actual tested source
because the assembly informational version embeds the Git commit. The work
branch was restored at `32e754f50e880e61e5c9982c2ec42ae86c74f6f8` before archival.
This documentation commit does not replace the tested source identity.

## Build identity and scope

Tested and restored Godot tree: `cfe370846c78c10100fbcfc8d19965efd96be697`.
Tested and restored tools tree: `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`.

All three actual frozen and live runtime DLL byte hashes match the recorded
native-capture build. The real retry hash record is preserved byte-for-byte,
including historical absolute paths; the comparator uses only file basenames,
so those paths do not prevent portable archive verification. The accompanying
`frozen-assembly-comparison.txt` preserves the actual primary-rebuild comparison.
The separate three native proofs identify the same source, trees and DLL hashes.

Observed totals include 6,876 animation assertions, 22,394 played-flow
assertions, 214 unique script UIDs, four zero-warning/error build summaries,
45 ordered stage markers, 16 AI traces containing 3,425 independent legal orders,
six played-flow markers, and the unchanged normalized multiset of 41 warnings.
No error or leak diagnostics were found. Existing environment-readiness
messages remain existing diagnostics, not newly solved conditions.

## Comparison and provenance

`compare-gate.py` is a portable, read-only evidence comparator. It launches no
Godot process, import, build or game test. Default paths are relative to this
archive and its sibling accepted evidence archives. Its code and eight controls
were not relaxed for this result.

The immediate accepted behavioral baseline is the **cheer aggregate**, tested
source `6fa6f8aa12cf4586967955c6898b6dd1409d16ad`, with exact full-log SHA256
`727765291d562311f4f65bbcbaf0f7267a77b3aaf548aca89aa3bc46f2cc0df3`.
Its archived exit 0, source record, terminal marker, successful archive comparison
and all 45 ordered stage markers verify. The current 16 ordered AI traces,
six played-flow markers and normalized warning multiset match this cheer log
directly, not the fall log or older staged recovery.

The archer-hit `full-native-staged` archive is used **only** as ordered 45-stage
coverage provenance. Its log/plan hashes, runner exit, completion/source record
and each individual stage result/log hash verify. It remains a staged recovery,
never a single-process aggregate.

Only assertion totals and UID counts in stage-marker strings are normalized.
Current metadata independently requires exactly 214 unique script UIDs. AI order
counts, digests, played outcomes and warning multiplicities are not normalized.
Warning normalization is limited to isolated temporary save roots. Silent
import/format completion depends on actual successful original-gate termination
and is never inferred from a partial log.

## Why the played-flow assertion total differs

The current total is 22,394 versus cheer's 22,496, a difference of -102.
Both `CampaignPlayedFlowChecks.cs` and `CampaignPlayedCarryChecks.cs`, and the
original gate script, are byte-identical between these tested commits. Their Git
blob IDs and SHA256 values are recorded in `assertion-count-review.json`.

`CampaignPlayedFlowChecks.Require` increments the counter on every call.
The defeat wait (lines 121–126) and carried `Until` helper (lines 382–389)
evaluate a deadline assertion once per awaited `ProcessFrame`. The unchanged
gate runs normal automatic playback without a fixed simulation frame rate, so
that total contains variable repeated polls, not just distinct semantic checks
or scenarios. All six played markers, every legal-order count and outcome digest,
and 14 additional progress/roster lines are identical. The suite still checks
every expected/presented receipt event in order before leaving each battlefield.

The log does not expose per-loop iteration counters. It therefore cannot assign
the exact 102-count difference among polling loops or establish performance.
No behavior, trace, event, outcome, ordering or hash requirement was weakened.
The navigation total also varies (9,817 versus 9,802) with unchanged navigation
sources and polling checks. Animation's increase (6,876 versus 6,669) comes from
the intentional new idle-contract coverage.

## Reproduce the read-only checks

From the repository root:

```sh
python docs/art-gravekeeper-idle-20261110/verification/full-native/compare-gate.py --output /tmp/gravekeeper-idle-gate-comparison.json
python docs/art-gravekeeper-idle-20261110/verification/full-native/test-compare-gate.py
```

Add `--frozen-dir` and `--runtime-dir` to verify actual DLL bytes when available.
For external evidence, use `--log`, `--exit`, `--source`, `--source-status` and
`--hashes`. `--baseline` changes only staged coverage provenance, never the
pinned immediate cheer aggregate.

Return 0 means complete and every requested check passed. Return 1 means
completed but mismatched. Return 2 means no final exit record. Missing or
malformed required completed evidence errors rather than passing.

All eight isolated controls pass: equivalent synthetic complete fixture,
changed trace, missing exit, missing terminal, nonzero exit, old UID count,
wrong tested source and wrong assembly record. Actual unittest output is in
`comparison-controls.txt`. Synthetic fixtures do not establish runtime success.

This gate covers headless contracts and the played opening, not export,
complete-campaign or realtime-performance acceptance. Sampled native visual
acceptance is documented separately in `../native/independent-final-review.md`.
