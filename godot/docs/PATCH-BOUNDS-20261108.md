# Shared exact patch bounds, 2026-11-08

## Measured problem and implementation

The existing C04 timing-marker fixture exposed repeated surface preparation work.
Exploratory instrumentation counted 1,112,139 per-surface scans of patch vertices
in one transition. Three simpler instrumented runs measured roughly 509–530 ms
inside 305 surface rebuilds; creating/freeing Polygon2D nodes was only about
52–78 ms of that. Node retention was therefore rejected as the first fix.
Detailed counters add overhead and are diagnostic, not benchmark results.

BoardSurfacePatch now computes its exact projected minimum/maximum once when its
vertices are assigned. PaintedSurface compares these extrema with its own bounds
instead of rescanning the shared vertices. The original strict-separation test
is unchanged: touching edges still reach the native clipper. Extrema are stored
as two vectors, not reconstructed using Rect2.Position + Size, avoiding another
rounding step at an edge. Vertex-replacing record copies recompute the extrema;
depth-only copies preserve them. Production vertex collections are immutable
read-only wrappers over newly created projection lists.

BoardProjection remains the geometry owner. Exact clipping, geometry/UV order,
interval cache publication, pointer/alpha behavior, synchronous Polygon2D
creation/freeing, unbounded surfaces and cliff processing are unchanged. There
are no retained native arrays or additional gameplay writers. No actor art,
action duration, release, support contact, game rule, map or save format changed.

## Controlled headless comparison

The same lawful 91-action C04 prefix and final order were executed serially with
uninstrumented baseline 0e7861b2 and the candidate source hashes in the evidence.
The target remains swordsman 12, attacked by gravekeeper 9. The three existing
poses are only 50/50/50 ms timing markers, not new or accepted recoil artwork.

- Fixed60 baseline wall times: 1434.34, 1602.37, 1419.75 ms.
- Fixed60 candidate wall times: 1001.05, 989.35, 1162.97 ms.
- Median: 1434.34 → 1001.05 ms, approximately 30% lower in this fixture.
- Two ordinary-delta headless runs per version also completed. Their raw times
  and samples are retained; this small sample is not a latency guarantee.

The wall interval includes synchronous transition preparation and settlement,
not startup, build or resource import. These are CPU-side headless observations,
not FPS or a rendered game benchmark. Every run retained the original before/
after/event hashes and all seven ordered receipts. Fixed60 sampled the same
0 → 1 → 2 → 3 → 0 sequence in both versions.

**The realtime missing-marker issue is not solved:** both versions still sampled
only 0 → 1 → 0 with maximum process delta near 150 ms. Reduced CPU work does not
prove every pose is sampled or displayed. No timing was stretched to hide this.

## Verification checkpoint

Focused tests passed: exact full-clip geometry/UV/depth/picking oracle, elevated
surface registration, 18,032 assembled-ground assertions and 3,111 animation
assertions. Additional guards cover exact extrema after repartition, vertex-
replacing record copies, depth-only copies and 100 readers without rescanning
vertices. The late-failure fixture supplies valid indexed vertices for initial
bounds, then throws on enumeration during geometry preparation; it still checks
atomic cache/node preservation and recovery.

Independent inspection found no correctness defect. Deliberate stale-record-copy
and shrunken-broad-phase controls, final restored oracle and the full unchanged
native aggregate are recorded in the adjacent verification files. Until a final
aggregate exit is recorded, this remains a focused checkpoint, not a full pass.

No graphical display is available. The attempted syscall diagnosis itself was
rejected by the environment (`ptrace: Operation not permitted`); it was not
bypassed. No PNG, Movie Maker, post-draw viewport or actual screen acceptance is
claimed. The earlier diagnostic's graphical mode remains unverified.

## Evidence

`godot/docs/verification/patch-bounds-20261108/` contains the comparison, source
hashes, build/runtime logs, negative-control results and retained scripts. The
existing `tools/godot/hit-diagnostic/` tool still documents the fixture and its
limits. The detailed exploration logs support where to investigate; only the
uninstrumented serial comparison is used for the timing claim above.
