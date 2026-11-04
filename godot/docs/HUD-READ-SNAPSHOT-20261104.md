# Revision-owned HUD read snapshots

Baseline: `cbebfe540a6569c1ab8dfed342dcbe9f447e1dfd`.
Implementation checkpoint: `db374184a2acc581a862e5406e56d238e10f3218`.
The following checkpoint `f42ead95f` only formats the new checks; the non-whitespace characters are identical.

## Evidence and scope

Ordinary map hover calls `GrayFlagSession.ProjectHud` when the pointed cell changes.
The existing implementation cloned the entire battlefield before every projection,
even when no order had advanced the session. On the unchanged larger C03/C07
openings, that detached copy allocated 1.94/2.25 MB per call. It accounted for
77%/79% of selected-unit HUD allocation and 91%/93% of idle-ground HUD allocation.

The controlled benchmark uses the shipped C01, C03 and C07 level documents and
actual game assemblies. It covers idle, friendly inspection, sixteen distinct
selected ground cells, pending destination, and legal attack targets. No weapon
target exists at those openings, so target fixtures dispatch 23/24/27 legal orders
before measuring. These are authored-level engine sessions, not carried-roster
campaign saves. No terrain, unit, rule or target was fabricated for the benchmark.

This follows Godot's recommendation to measure a representative bottleneck and
remeasure the change, while distinguishing CPU work from rendering or frame rate:
[Godot general optimization guidance](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html).

## Ownership and change

`Battle` remains the sole writer. `GrayFlagSession.ProjectHud` privately retains one
detached read snapshot with its session revision, rather than cloning that same
revision on every hover. It still computes the complete HUD for each input. The
cache does not retain a previous selection, cursor, busy state, binding or output.

The entry is removed before invoking query collaborators and published only after
constructing the complete output and checking the revision again. A thrown query
or a reentrant committed order cannot publish a partially projected read state.
Successful dispatch and undo advance the authoritative revision; a new session
naturally has its own entry. No separate invalidation list is required.

Public `Snapshot`, board snapshots, selection-query state/movement fields, and the
necessary hypothetical `PreviewAttack` clone are unchanged. Widgets receive only
detached immutable HUD values. Maps, scenes, art, battle rules and save semantics
are unchanged. The trade-off is retention of at most one private HUD snapshot per
live session; cold/revised-state setup still takes a snapshot.

Query callbacks borrow state read-only. As with the existing revision-bound
selection queries, a custom callback that successfully mutates or retains and later
changes this borrowed state violates its query contract. This change does not add
per-call clone/hash enforcement or a special path for the default registries.
Failure recovery is explicitly tested without swallowing the original exception.

## Controlled A/B

Both sides run the same compiled probe, frozen content/localization, legal order
prefixes and selections. Each case has 80 warmups and seven rotated batches of
128 calls. Setup, serialization and equality checks are outside timing. The two
baseline runs agree on every shared snapshot/HUD allocation median. Baseline
artifacts and DLL fingerprints remain intact.

| Scene and HUD input | Baseline bytes/call | Candidate bytes/call | Reduction |
|---|---:|---:|---:|
| C01 idle ground | 625,000 | 114,792 | 81.6% |
| C03 idle ground | 2,138,776 | 197,800 | 90.8% |
| C07 idle ground | 2,414,304 | 163,920 | 93.2% |
| C01 selected ground | 1,012,628 | 501,940 | 50.4% |
| C03 selected ground | 2,527,148 | 585,724 | 76.8% |
| C07 selected ground | 2,842,280 | 591,384 | 79.2% |
| C01 legal attack target | 1,593,560 | 1,069,944 | 32.9% |
| C03 legal attack target | 4,699,984 | 2,743,016 | 41.6% |
| C07 legal attack target | 5,349,824 | 3,084,856 | 42.3% |

All **288 complete per-cell HUD projections and six complete battlefield states**
are identical. Comparisons include every row, intent/action, enabled/selected flag,
hint, portrait and gauge, not only a summary. Session revision, action/event logs,
detached query inputs and public snapshot isolation remain unchanged.

All fifteen unchanged-method allocation controls (`Snapshot`, query construction,
`PreviewAttack`) match exactly. Rebuilding changed Core assembly version metadata;
a separate inspection confirms all 5,317 method signatures, IL and exception
metadata are unchanged. The source diff contains no Core implementation change.

Selected-HUD batch-median times were 3.321 → 2.170 ms (C01), 7.951 → 3.983 ms
(C03), and 6.508 → 2.200 ms (C07). Wall timings remain host-sensitive, and unchanged
control timings also vary. Allocation reductions and full-output equality are the
strongest evidence. These numbers are warmed managed Debug microbenchmarks, not
FPS, input-to-pixel latency, first-use loading or graphical acceptance.

## Verification

- Debug build: exit 0, zero warnings/errors.
- Editor import: exit 0, strict diagnostic gate passed; generated script UID kept.
- Native `TacticalHudChecks`: exit 0, 651 assertions. Its new lifetime checks cover
  alternating input/busy/binding, retained outputs, separate battles at revision 0,
  public snapshot isolation, refused commands, warm and cold exceptional reads,
  face/undo/end-turn, stale entry and a callback committing during projection.
- The unchanged formal C03 map also has a warmed native allocation guard. Eight
  distinct cells allocate 1,545,600 bytes idle and 4,547,520 bytes selected, stable
  over all three samples. Native resource parsing and runtime are distinct from
  the standalone A/B, so these absolute byte values are not mixed between runs.
  Cold initialization is printed separately and excluded from the warm claim.
- The initial whitespace check failed on formatting in the new test file. The
  official formatter corrected only that file. The failure is retained, not
  presented as a pass. The final aggregate formatting check passed.
- An isolated copied project passes the green lifetime/allocation control, then
  detects all six independently built negative controls: clone each hover, ignore
  revision, retain a failed warm read, omit the final revision check, omit the
  stale-entry check, and publish a cold cache before successful projection. Each
  negative builds successfully and exits 1 at its intended assertion. Production
  files are never mutated; the copied source is restored afterward.
- The complete `tools/godot/verify-native.sh` aggregate passed continuously on
  source checkpoint `2010d27bdd6c87dea87c74923b057ad760b5e9a3`, from
  2026-11-04 21:49:26 to 22:35:52 UTC (46m26s), exit 0. All 15 Core groups,
  all 16 formal levels / 3,425 exact ordered action-event-receipt traces and final
  digests, 30 native scenes, formatting/static checks and editor import passed.
  All 31 strict native logs were retained; 40 warning headers exactly match the
  preceding full run, with no new warnings, errors, leak or crash diagnostics.
- The same aggregate includes the real New → story choice → C01 battle pointer
  route. Across all three samples, sixteen native mouse motions allocate
  10,231,496 bytes versus 16,511,176 in the preceding full run, a **38.0%**
  reduction. Retained controls, correct terrain facts, dossier pointer/keyboard
  interaction and canonical state guards pass. Campaign navigation finishes
  with 9,108 assertions and zero failed groups.
- The native pointer sample times were 122.967–154.990 ms per sixteen motions,
  versus 105.883–129.218 ms in the earlier aggregate. These separated host runs
  do **not** demonstrate a native latency improvement; the claim is reduced
  allocation. No frame-rate or input-to-pixel conclusion is drawn.
- ExportRelease build: exit 0, zero warnings/errors. It did not change any
  verified Debug assembly. This is compilation, not a packaged export.

The final report update is documentation-only; no implementation changed after the
complete aggregate. All earlier failed formatting and deliberate negative-control
results are kept separately from the successful final run.

The benchmark and verification artifacts are preserved in the task workspace as
`empire-formal-hud-query-probe-20261104`, `empire-formal-hud-query-candidate-20261104`
and `empire-hud-read-state-20261104`. The native guards are repository tests, not
an external benchmark required to build or run the game.

No real-display visual acceptance or packaged export has been performed here.
