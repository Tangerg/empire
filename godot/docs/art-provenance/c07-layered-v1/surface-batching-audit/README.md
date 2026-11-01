# Same-support UV batching — static correctness and measured cost

## Change

Only `CompiledSceneSurface.Instantiate` drawing changes: all triangles belonging to one existing support node use one Polygon2D with explicit ordered index arrays. Vertices remain independent, preserving UV seams and each primitive's Tint through VertexColors. Color stays white; no second tint multiplication. No support nodes, contact, alpha sampler, material coverage, pointer, site, ground partition or game rule changes. No cross-support grouping, transparent blending removal, overlap deduplication or mesh re-triangulation.

This uses the same mechanism already present in ProjectedGround. The compiled private triangle/UV/contact data remains the only source for draw and Pick. The audit reader now reads Polygon2D.Polygons indices rather than wrongly treating the concatenated vertex pool as a single contour. Old standalone probes that still operate on individual polygon nodes are not automatically suitable for this representation.

## Correctness

- Fresh production build: no warnings/errors. Production SceneSurfaceChecks now has14 assertions, including shared-support batching, independent UV vertices and nonwhite primitive Tint.
- 45 support entries and846 triangle vertices retained. Exact migrated position, UV, Tint and depth maximum errors all0;2961 material samples have0 coverage mismatch. Polygon nodes for the fence:282 unbatched formal →45 batched (historical wrapper144).
- Actual Torren back/front frames at zoom4 are pixel-identical to the unbatched formal renderer, including the unchanged caption. This is rendering preservation, not a new art-quality claim.
- Synthetic native-alpha/transparent-hole/two-Tint overlapping triangles: six full-frame pairs at zoom1/1.25/2 and integer/half-pixel translation are pixel-identical. These images are explicitly code-native material diagnostics, not game artwork.
- Reversing the overlapping primitives is a meaningful negative:406 changed pixels outside the caption, maximum channel difference34. Interior pixel(595,329) changes from[147,131,134,255] to[177,121,100,255], proving the test detects draw-order/color errors.
- Wrong material, duplicate primitive and independently early/late support controls still fail their target assertions; the positive actual-character probe passes. None executes engine actions.
- Dedicated initial-state-only C01–C06 repeated-render regression passes with unchanged canonical digests and zero rendering issues. It has no commands, AI, campaign saves or route progression. Current paused/entry/source C06 JSON hashes again match v53. No resumed C06/C07 gameplay.

## Measurement

Native llvmpipe,1180×760,same one-object scene, Torren static rear pose,zoom4. Each run has10 seconds warmup plus20 seconds continuous engine-frame samples, with no screenshot/readback/build/import during sampling. Raw samples and method source are retained. Cold construction time is recorded separately and is not a steady-state FPS result. A frozen cfd5f370 production DLL provides the formal unbatched control; its exact SHA is recorded, not a reconstructed approximation. No user GPU was measured.

First ABBA migration baseline (historical wrapper/formal/formal/wrapper): draw calls180/318/318/180, frame medians33.33/36.11/36.66/34.72ms. This identified the extra per-triangle draw cost.

Second ABBA uses the **same formal input**, unbatched/batched/batched/unbatched:

| Run | Draw calls | All ground Polygon nodes | Frame median ms | Frame P95 ms |
|---|---:|---:|---:|---:|
| Unbatched1 |318|321|50.60|116.59|
| Batched2 |81|84|32.94|66.55|
| Batched3 |81|84|30.95|50.04|
| Unbatched4 |318|321|32.53|58.42|

All runs retain39 depth partitions,34579 ground patches and unchanged canonical initial state. Draw calls fall by237(74.5%); the reduction in rendering complexity is proven. The large first/last unbatched timing drift overlaps batched timings, so **a stable FPS gain is not proven**. No whole-C07/C05 performance acceptance, target-device guarantee or threshold was inferred. The high ground-patch cost remains explicit; it is not reduced by collapsing valid contacts.

## Boundary / next work

Official C07 still uses its original scene; only the unbound isolated preview exercises the new surface path. This bounded batching pass is closed; do not keep widening it into speculative cross-support optimizations. Next migrate a closed component only after comparing its exact UV clipping and local support authoring semantics, using this same compiler rather than another runtime wrapper. Full scene coverage, northern lookout art, material consistency and separately authorized real gameplay remain unfinished.
