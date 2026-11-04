# Realize registered ground after final depth assignment

## Reproduction and scope

Baseline: `975b4bd7ad245e55f7b29c4224bcba82a1a209fd`.
Implementation: `bd8bb9e93844cb22d227e5caba98282ee3e588b7`.

The actual application path is New Campaign, seven story beats, the
`rush-north-hill` choice, Start Battle, and two returns through the system-menu
camp confirmation followed by Start Battle. The probe emits the actual native
button/confirmation signals, disables automatic game processing to hold the
opening state, and waits two scene-tree frames outside each timed callback.
Each process uses fresh temporary XDG data, configuration and cache directories.
No real user save is touched.

The original C01 entry builds 98 `PaintedSurface` instances: one base floor and
97 existing object contacts. `AssembledGroundView.Render` eagerly built all their
polygons before the board knew the final standing-depth cuts. The immediately
following `PrepareDepthPartitions` emitted a change and rebuilt all 98 again.
Three instrumented entries attributed 1,296 / 1,206 / 1,133 ms to initial builds
and 1,243 / 1,209 / 1,352 ms to the second batch. These nested CPU timings identify
the duplicated work, not an independent total or a frame-rate measurement.

## Ownership and implementation

The board already owns the sequence: ground preparation, final depth partitions,
standing Z assignment, then `ApplyDrawOrder`. Registered ground now materializes
its floors at that existing final step. `Render` validates registration and
prepares depth entries; the prepared projection is published only after success.
The separately tracked surface projection records successfully completed floors.

The same contract applies to `AssembledGroundView` and `PaintedGroundView`.
Direct ground callers were migrated. `PaintedSurface.Create` remains eager for
standalone and dynamic-floor callers. Its geometry algorithm, UVs, clipping,
picking, texture borrowing and later depth-change subscription are unchanged.
The material compositor, dynamic surfaces, camera, application refresh sequence,
source art, maps, rules and save semantics are unchanged.

Assembled replacement stages the complete base/contact set before freeing any
old floor or changing contact placement or sibling order. Factory failure frees
all staged subscribers and keeps the completed floors and textures. Successful
installation precedes compiled-surface draw grouping, which depends on the
actual native sibling order. No global cache or second terrain owner is added.

This follows Godot's guidance to [measure the relevant bottleneck before tuning](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html)
and to preserve [resource sharing and ownership](https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html).

## Exact captured output

The unchanged and candidate Debug DLLs ran under Godot 4.6.3 Mono headless.
A frozen probe captured the ordered 957-node board after each entry: native
Variant-precision local/global transforms, Canvas visibility/colors/Z/filter
properties, complete Polygon2D geometry/UV/index/vertex-color arrays, Sprite
frame/region/offset/flip data, texture paths and dimensions, and actual image-byte
hashes. Only engine-generated numeric node-name suffixes were normalized.

All corresponding captures match byte for byte: initial candidate parity run
(3 entries), three alternating original/candidate pairs (18 entries), and the
symmetric counter/allocation pair (6 entries). Each has 19 visible units,
no dynamic structures and no rendering issues. The observed first-entry versus
reentry global transform difference is preserved, not removed for comparison.

- First-entry binary SHA256: `196a2e4219da7b0b1a069dbf065f72ff5be4967a03c3a851fba7d653bf24b4db`.
- Each reentry: `e604dd8a7dae148e4fbbd9fbfe2a8a20425adbe8f79ce48914e1fc23059239d1`.
- Frozen probe: `ff0fd9898c042cf803822c97b2e6541916fb587d04e13cd02ef0f846e66291df`.

These are exact captured native-data comparisons, not GPU screenshots or visual
acceptance. No usable graphical display or packaged export was available.

## Measured work and allocation

Identical isolated instrumentation was applied to original and candidate source,
using each build's matching Core DLL. Both builds completed with zero warnings
and errors. Each native process completed the same three-entry path and exited
zero. Counter and allocation instrumentation patches are byte-identical.

Each original entry recorded 0 / 196 / 196 rebuilds after Bind / Ground / Dynamic
configuration. Every candidate entry recorded 0 / 98 / 98. Both final trees have
98 actual surfaces. The change removes the second build, not any surface.

`GC.GetAllocatedBytesForCurrentThread` surrounds synchronous
`BeginBattle` through `ShowBattle`. It excludes the preceding autosave and
following evidence capture. It includes small symmetric instrumentation overhead
and measures managed allocation on that thread, not native/GPU allocation,
retained memory or peak resident memory.

| Entry | Original bytes | Candidate bytes | Reduction |
| --- | ---: | ---: | ---: |
| First | 254,656,112 | 230,577,224 | 24,078,888 (9.455%) |
| Reentry 1 | 246,993,416 | 222,317,808 | 24,675,608 (9.990%) |
| Reentry 2 | 246,693,080 | 222,305,416 | 24,387,664 (9.886%) |

This is one symmetric process pair with three entries, not a hardware survey.
Both processes also passed the complete captured-output comparison. No source
materialization or build overlapped those runs; log polling and one external
text scan did occur. External scans do not directly allocate on the measured
managed thread, but the runs are not claimed to have perfectly isolated timing.

## Timing limits and all alternating samples

All milliseconds below are retained, including slow outliers. Columns are first
entry, first reentry and second reentry.

| Pair | Variant | First | Reentry 1 | Reentry 2 |
| --- | --- | ---: | ---: | ---: |
| 1 | Original | 6,235.291 | 5,721.215 | 4,503.698 |
| 1 | Candidate | 22,208.869 | 4,700.016 | 5,267.373 |
| 2 | Original | 7,462.628 | 6,264.624 | 5,847.518 |
| 2 | Candidate | 6,630.187 | 5,763.429 | 67,983.662 |
| 3 | Original | 10,184.943 | 5,565.601 | 5,523.631 |
| 3 | Candidate | 7,365.957 | 3,979.975 | 3,351.348 |

Read-only source materialization for the later counter experiment overlapped the
second candidate process and is a possible CPU/I/O confound. Other scheduling,
GC and I/O effects were not independently measured. Evidence capture is outside
the callback timing, uses the identical probe, and retains no native image/node
objects, but this does not rule out indirect tail effects. These samples do not
establish a stable latency improvement. No FPS or graphical-performance claim is
made. The acceptance evidence is reduced repeated work, lower measured managed
allocation and preserved captured output.

## Regression and validation status

The lifecycle guards cover preparation without premature floor creation,
completion when partition cuts do not change, repeated completion without new
polygons, synchronous later depth changes, failed first and replacement builds,
retry, preservation of committed floors/order/placement, pending unload, and
rejected registration retaining the previous prepared projection. Existing
source scaling, texture/cache lifetime, complete UV/geometry/picking and material
negative controls remain intact.

Focused checks passed with clean process teardown: AssembledGroundChecks (18,032
assertions on the saved checkpoint), PaintedSurfaceChecks,
PaintedSurfaceBroadPhaseChecks (8,129 exact reference comparisons),
SceneSurfaceChecks (60), DynamicStateChecks (745) and PresentationChecks.
Debug build and official whitespace formatting passed.

Isolated mutation checks and the complete aggregate are still pending at this
document checkpoint. Do not treat the focused passes as a complete gate.
