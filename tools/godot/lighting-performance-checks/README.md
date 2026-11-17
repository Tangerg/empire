# Native lighting raster output

Run `bash tools/godot/lighting-performance-checks/run.sh [absolute-godot-project-directory]`.
This runs `LightingRasterParityChecks` on native pixel conversion, shadow raster
output and composed RGBA images. It is not a desktop frame-rate benchmark.
Use `tools/godot/lighting-checks/check-lifetimes.sh` for cache and shutdown
coverage.

The measurements below describe past snapshots. The old exact convolution and
frozen synthetic corpus scene is no longer a verification gate.

## Recorded paired evidence (2026-10-20)

Three alternating baseline/optimized fresh-process pairs ran with other local native
suites paused. Both used the same frozen GrayFlag source/art snapshot, Debug build and
Godot 4.6.3 Mono toolchain; only `ShadowRaster.cs` changed.

- First board baseline: 5442, 5392, 4936 ms; optimized: 4015, 4017, 3778 ms.
  Each paired run improved by 23.5–26.2%. These are load/composition times, not frame rates.
- First board managed allocation: approximately 352.74 MB baseline versus
  329.66–329.72 MB optimized. This measures allocated managed bytes, not peak memory,
  retained memory or native image/texture storage.
- Eight cached projections per launch averaged 1.87–2.50 ms baseline and
  2.12–2.91 ms optimized. No warm-path improvement is claimed.
- New imported figure frames still took approximately 90–120 ms in the optimized
  snapshot. Reusing an already composed frame took 0.006–0.038 ms. First-use frame
  latency and multi-second board composition remain explicit responsiveness gaps.
- Every launch passed all 195 assertions, kept frame/cache counts stable, and exited
  with status zero and no resource/error diagnostics.

Baseline raster SHA256:
`8cb8fab5a5d2032d26bb1f88df631a4435f74b5a0b8930322f33a3cf8ea1186f`.
The frozen composition/cache inputs were unchanged between paired launches:
`LayerFilterComposition.cs` SHA256
`f327e0950bfa7c811de11114fe0843a9011760acdfa5d86ed20d06195086e052`, and
`SpriteShadows.cs` SHA256
`76952aaae4062ea47fb0ae7f43264314d3bd5f94b6f9566090ce08086f5d31c9`.

Initial contended diagnostic profiling identified 81 shadow bakes covering 5,011,409
padded pixels. Their managed allocation was about 183.65 MB, with alpha reads 305 ms,
Gaussian convolution 2205 ms, chained sampling 867 ms and native output writes 336 ms.
This is bottleneck evidence, not a controlled before/after performance comparison.

The frozen optimized tree also passed the repeated-exit gate: six animation launches,
three lifecycle launches (nine real-session load/restart/queued-exit cycles), real screen
bindings and all 1537 source lighting contracts, without shutdown leaks or errors.
A later combined renderer working-tree run passed the 195 performance assertions at
3688 ms/174.21 MB for the first board and 84–104 ms/1.78–1.99 MB for new figure frames.
It includes the independent allocation-free cutout-coverage loop and renderer changes;
those results are not attributed to this raster optimization or substituted for the
frozen pair. Visual desktop acceptance remains pending.


## Exact bulk/SIMD and coverage batch

### Exactness and ownership

- `RasterPixels` takes a temporary RGBA8 byte snapshot once per input image.
  Other formats retain native `GetPixel`; no source image is converted or mutated.
  Writes preserve Godot's double-precision multiply by 255.0, clamp and truncate,
  rather than float multiplication or rounding. This matches the official
  [Godot 4.6.3 image implementation](https://github.com/godotengine/godot/blob/4.6.3-stable/core/io/image.cpp).
- SIMD runs across independent output pixels. Each lane visits Gaussian taps in the
  original order with separate multiply and add operations, not a parallel reduction,
  approximation or fused-multiply-add rewrite. Edges, tails and unsupported hardware
  retain scalar calculations. All vectors and arrays are local to one bake.
- Coverage can return one only when there are no cutouts and both extreme original
  subpixel sample locations lie inside the layer. It tests the actual rounded sample
  positions, not an approximate pixel bounding box. Other cases use the unchanged
  16-sample coverage. Tests probe neighboring float values on all rectangle boundaries,
  large coordinates, tiny cells, null/empty/full/overlapping cutouts and fractional scales.
- The sole board cache and its disposal are unchanged. Temporary byte snapshots add
  managed allocation; they do not become another persistent cache or resource owner.
- The non-RGBA8 references cover shadow alpha and compound-image sampling. Complete
  `Filter` retains the existing native `BlendRect` same-format requirement; this batch
  does not silently convert or expand that contract.

### Feasibility measurements and limits

An isolated source/art snapshot with the first raster batch and allocation-free
cutout loop was held fixed. These initial sequential measurements are diagnostic,
not a statistically controlled speed guarantee or desktop frame-rate acceptance:

- Baseline first board: 3866 ms, approximately 174.08 MB managed allocation.
- Bulk raster only: 3698 ms, 206.78 MB.
- Bulk raster plus exact-order SIMD: 2698 ms, 206.77 MB.
- Also bulk compound-image sampling/output: 2470 ms, 209.70 MB.
- Gaussian stage: approximately 1502 to 623 ms; alpha reads: 225 to 39 ms.
  All 81 observed source raster inputs were RGBA8.

Subsequent instrumented coverage runs overlapped other verification, so their timing
is explicitly not a quiet comparison. The exact interior fast path reduced a measured
new walk-frame coverage portion from 34 to 6 ms, and full composition from 57 to 32 ms.
The corresponding full new-frame diagnostics were 34–46 ms, while first board was
approximately 2176 ms. Total managed allocation remained approximately 209.8 MB:
about 35.7 MB more than the first-batch-plus-coverage baseline, due to temporary byte
snapshots. These are allocated bytes, not measured peak or retained memory.

Cold composition and first-use frame latency remain performance gaps. No 16 ms frame
budget, desktop responsiveness, GPU performance or visual acceptance is claimed.
Existing cached frames remain sub-millisecond. Further changes should be driven by
fresh profiling, not inferred from the remaining total.

The initial bulk/SIMD variants and final coverage variant preserved the frozen raster
SHA256. Both accelerated and hardware-disabled runs passed the expanded float and
RGBA/boundary suites with clean exits. Deliberately weakening the coverage fast path
to test only its first sample was rejected by compound RGBA equality. Renew combined
source-lighting and repeated-lifetime verification after integrating this batch;
first-batch lifecycle evidence is not a substitute for that combined gate.
