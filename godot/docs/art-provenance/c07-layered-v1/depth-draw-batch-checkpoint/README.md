# Draw-only batching checkpoint

2026-11-02. Baseline 1c6c0435e1e52c57739b970d82958cea9b39a8cd. C07 selected assembly remains incomplete and runtimeReady=false; official scene unchanged. No C06/C07 action was advanced.

Compiled surface primitives remain the sole geometry/UV/alpha owner. Physical pieces, support, Visual and Pick stay separate and ordered. After actual standing Z/order assignment, only consecutive same-instance, same-Z, same-transform/material child draws are grouped. Extra children/shadows and intervening siblings are barriers. A changed grouping rebuilds from compiled source primitives; repeated rendering does not append geometry.

Verified: fresh production/external builds; 49 surface contracts; real GPU half-transparent overlap exact RGBA before/after/repeat, reversed-order negative differs by 8520 bytes, real intervening sibling remains pixel-exact. Selected assembly structure and repeated RenderState pass. First whole-field same-camera image is byte-channel exact (0 changed pixels). Actual draw calls 3361 to 2298 (-1063, 31.6%). 4524 physical entries and 7459 board nodes unchanged.

The initial baseline and optimized runs were not a controlled ABBA pair: optimized frame median about 85.1ms does not establish FPS improvement. Full ABBA and C01–C06 static regression remain pending at this checkpoint. Shared llvmpipe measurements are not user-hardware FPS. No replay, real gameplay or final art/performance acceptance is claimed. Failed first constructor build retained as development evidence, superseded by successful fresh builds.
