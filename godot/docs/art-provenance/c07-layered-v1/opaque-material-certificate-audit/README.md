# Exact opaque-material rectangle certificates

2026-11-02. Initial selected C07 only, official scene unchanged. No gameplay/action/AI/save commands. This optimizes compilation, not gameplay rules, physical geometry, alpha thresholds or final art.

## Measured cause

Uninstrumented startup took 20.126s, including 16.642s in ConfigureGround. A separate sparse diagnostic build of 0a3f407c measured 36 surface compiles at 19.185s, of which 18.898s was exact opaque triangle/texel validation. Instance construction was 95.8ms, pixel snapshot plus DetectAlpha 88.6ms, texture loads 581.5ms. Instrumentation overhead/shared-host variation is explicit; these are not interchangeable FPS measurements.

## Ownership and algorithm

`CompiledSceneSurface.Material` already owns one `Texture2D.GetImage()` snapshot per shared texture per assembly compile. A lazy integer summed-area table now counts texels below the unchanged 0.96 opaque-material threshold. Only a rectangle containing **zero** failing texels certifies a triangle. Any nonzero count uses the original exact triangle/texel-intersection path, including degenerate UV lines and points. Vertex, UV domain, finite and geometry validation remain independent. Native-alpha sprite/Pick sampling is not governed by this material threshold.

The array is private, derived once, and never edited after construction. It cannot outlive its Material owner: the existing local `using Materials` covers success/failure, disposes pixel snapshots and clears the registry; Material.Dispose drops the array reference. Compiled geometry keeps primitives/texture, not Material. No global cache, additional Node, external invalidator, exported tuning knob or runtime profiling mode was added. No source image decode duplicates Godot's format handling.

Space cost is `4*(width+1)*(height+1)` bytes per actually indexed partial-alpha texture. The selected scene upper bound is 9 distinct textures / 56,713,484 bytes (about54.1MiB), coexisting briefly with pixel snapshots. C# collection need not occur immediately. A diagnostic-only forced collection keeps compiled geometry alive and confirms both disposed Material and prefix WeakReferences die; no forced GC exists in production.

## Godot 4.6 and project guidance reviewed

- [General optimization](https://docs.godotengine.org/en/4.6/tutorials/performance/general_optimization.html) distinguishes loading latency from frame cost and calls for measurement before/after changes. Our timers isolate loading; GPU evidence independently protects rendering.
- [CPU optimization](https://docs.godotengine.org/en/4.6/tutorials/performance/cpu_optimization.html) recommends targeting measured bottlenecks and notes profiling overhead. We did not delete slices merely because node counts looked large.
- [Lightweight data alternatives](https://docs.godotengine.org/en/4.6/tutorials/best_practices/node_alternatives.html) supports narrowly scoped data objects rather than unnecessary scene nodes. This temporary certificate is a plain owned array.
- [Texture2D.get_image](https://docs.godotengine.org/en/4.6/classes/class_texture2d.html#class-texture2d-method-get-image) returns a new image copy and warns against repeated GPU readback. The existing one-snapshot registry remains the owner; there is no per-frame readback.
- [Resource lifecycle](https://docs.godotengine.org/en/4.6/classes/class_resource.html) explains cached references and delayed C# collection. We release owned temporary image/array references, not live textures used by drawing.

The summed-area certificate and scene support system are project-specific, not claimed to be prescribed Godot algorithms. Their need comes from the measured asset workload; AGENTS/PROJECT_RULES single-owner, minimal API, no weakened assertions and explicit tradeoffs govern implementation. Independent read-only review found no blocker and requested the added delayed-first-query snapshot test.

## Verification status

60 source/material/draw contracts pass, preserving all previous49. Added exhaustive1296 closed rectangle comparisons, fractional bounds, final row/column, 244/245 alpha boundary, late source texture mutation before/after first certificate, bounding-box-only holes and interior low-alpha rejection. A deliberately always-true certificate fails the intended original transparent-material rejection, not fixture initialization.

First cold-process native result: ConfigureGround2.786s / initialRender6.202s, exact full-field RGBA equality to baseline, 4524 physical entries, 7459 nodes and2298draws unchanged. This is loading improvement, not a new FPS claim. Repeated cold-process and final selected/six-level static checks are pending until appended below. The first external lifetime harness had a syntax build error, corrected before its successful run; failed log retained.

## Repeated cold-process result

Second identical scene load: ConfigureGround2.876s / initialRender6.323s, again0 changed RGBA pixels. Neither run changes4524 supports/7459 nodes/2298 draws. Compared with uninstrumented baseline16.642s /20.126s, this is roughly83% less ground-load latency and69% less initial-render latency in this cloud setup. No frame-rate gain is attributed to the compile-only index.

Lifecycle check keeps the compiled geometry and its texture alive while dropping the temporary Materials scope; after diagnostic-only collection both materialAlive=false and prefixAlive=false. Review's delayed-first-query test also passes (60total contracts). Actual expected negative: `Expected surface rejection: Opaque surface material includes transparency` after an always-true rectangle certificate; failure is not build/setup.

Final C01–C06 initial-state/hash and12,590cell-contact picks exactly match the pre-batch baseline. Selected assembly repeated-render/site contracts and actual garden actor/wood-hole/roots/approach contracts pass. Source metadata and diff checks pass; six paused evidence hashes unchanged. Full-scene art, remaining city responsibility and final target-hardware performance are still open.
