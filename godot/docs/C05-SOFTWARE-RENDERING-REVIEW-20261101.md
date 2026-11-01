# C05 controlled cloud renderer review — 2026-11-01

**Not a performance pass; no production optimization adopted.**
After the actual T8 battle/settlement was saved, a separate read-only renderer
fixture used the real T7-human-end state with the same camera. It does not replay
AI or certify battle/HUD input speed. Every run warmed10 seconds then sampled20
seconds continuously, without F12 readback/import/build during the interval.
Actual captured viewport is1180×812; the window manager adjusted requested760.
Renderer is Mesa llvmpipe software rasterization, not a user's GPU benchmark.

| Variant | First FPS | Reverse-order FPS | Draw calls | Ground patches / partitions |
|---|---:|---:|---:|---:|
| Current depth strips |14.51|16.80|293|26942 /147|
| Unsplit diagnostic negative baseline |16.90|17.10|193|15518 /67|
| Historical painted scene |19.54|19.55|205|16568 /73|

Counts are stable; timing is variable. Do not attribute all low frame rate to
bridge strips from the first order alone. Unsplit is not an acceptable rollback:
its actual rear-rail alpha/Z regression is protected by native negative controls.

Diagnostic isolation only: hiding all RegisteredSurface nodes yielded24.27 FPS,
but accidentally also excluded two death markers; this is explicitly not a clean
ground-only control. Corrected ground-only exclusion retained markers and yielded
21.31 FPS/167 calls. Shadows-only exclusion yielded18.19 FPS/257 calls. A later
current control was10.76 FPS, reinforcing nonstationary cloud timing limits.

None is a shippable optimization: ground-only exclusion changes4108 non-label
pixels by more than5 RGB levels, mostly contact shadows and lower object edges.
All1600 C05 elevation values are0, yet simply bypassing its depth-composited ground
is not pixel-equivalent. The20 rail strips also have no wholly alpha-zero base
strip: edge strips retain a few A=1 pixels, mipmaps/linear filtering, and the first
strip owns the single contact shadow. No alpha was deleted to force a fast result.

Preserve current rendering and complete C06/C07 functional/visual work before a
bounded cross-level batching study. Future changes require actual pixel, feet,
shadow, actor/rail front/back, high/low-ground and cliff fallback regressions—not
quality loss, hidden layers or only lower draw-call assertions. Private recovery
backup retains raw samples, source fixture, launch scripts, logs and final images.
