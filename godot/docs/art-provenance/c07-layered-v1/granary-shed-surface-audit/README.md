# Granary shed: formal surface transfer 

This batch advances the same selected assembly; the official C07 scene is not switched.
All C07 probes are initial-state/display-only. No moves, commands, AI, turn ends,
save writes, or campaign outcome are exercised.

## Factory material boundary

The old factory rejected all materials whose pixels had no transparency, including
RGB textures used by explicitly bounded polygons. The original guard fails in
`rgb-surface-old-guard-negative.log` after the RGB surface acceptance marker,
not during fixture setup. Sprite silhouettes still require alpha. Explicit surfaces
derive their shape from the compiled geometry; native-alpha coverage still additionally
samples mapped texture alpha.

The initial fix passes AssembledGroundChecks (12495 assertions) and
SceneSurfaceChecks (22). These include true RGB sprite rejection, RGB polygon interior
draw-node and endpoint Pick checks, a geometric gap, and native-alpha hole preservation.
Final fresh results and native GPU evidence follow below.

## UV numerical transfer, not a new material design

The original roof UV corners are exactly within [0,1535] x [0,1023].
Geometry2D intersection outputs use float coordinates; the first reported clipped
world point (301,123.63414) has double recomputed third barycentric weight
approximately -1.11e-7. Thus double interpolation alone cannot eliminate the
quantization of the clipped point itself. The original exporter produced 154
negative UV endpoints, minimum about -0.000198 texels.

The temporary half-texel inset was rejected and is not the selected source.
The original world geometry, source UV corners, and support curves remain unchanged.

The offline ClippedTriangleUv mapping accepts only source-triangle intersections
within each supplied point's one-float-ULP coordinate uncertainty, converts that
bound analytically to barycentric bounds, rejects clear exterior/nonfinite points,
and then uses nonnegative normalized convex weights. Exact original triangle
vertices retain their exact UV values. Runtime bounds/opaque-alpha checks remain strict.

ClippedTriangleUvChecks: 629 assertions; all 154 original negative endpoints
reproduced and corrected; far exterior points rejected; exact vertices and edge
midpoint retained. Maximum correction versus old float arithmetic is 0.00040943627
texels. This is not a claim of bit-identical old wrapper UV or GPU pixels.

## Scope and existing debts

The shed roof/fascia and each of four posts have their own source contact.
Left / near feet were visible in the original. Right / rear hidden feet are
explicitly authored structural design, not recovered original observations.
No legal original C07 actor roof-intersection bug was established; this transfer
does not claim to repair such a gameplay bug. Large grain stock and column box
are not included yet. Roof pattern density/flatness and contact-shadow integration
remain previously recorded art debt.


## Closed transfer and final validation

- Same selected assembly: 22 instances, 5 semantic sites, 1698 depth entries.
  The shed adds six independent material/support components, no extra site owner.
- 4581 drawn triangle vertices match exact original clipped world coordinates;
  the historical wrapper differs at some X coordinates by one ULP (max 0.000030517578).
- All 3097 resource UV vertices exactly match the closed-triangle offline mapping.
  Maximum UV difference from the old wrapper is 0.0004600468 texels across all six
  components; source UV corner definitions remain unchanged.
- 17640 source-space coverage samples have zero differences. Depth and tint exact.
  305 batched polygon nodes replace 785 old polygons; no FPS claim.
- Ten material/void witnesses, all four independently owned post feet, existing
  granary door (35,14) and approach (36,14) pass. Wrong roof-envelope post support
  reaches and fails its precise independent-foot assertion.
- Two native GPU comparisons: 2.5x close 56 changed pixels / max channel delta 1;
  1.25x context 26 changed pixels / max 1. They are not bit-identical.
  Context overscan is an explicit diagnostic camera, not the default camera.
- Fresh production and external probe builds: zero warnings/errors.
  AssembledGroundChecks 12495; SceneSurfaceChecks 34; closed-triangle checks 629.
  C01–C06 initial repeated-render checks: zero issues, state unchanged.
  No broad gameplay/replay suite run, and no C06/C07 action execution.
- Six exports reproduce byte-for-byte. Six paused C06 save/evidence hashes unchanged.

## Immutable material snapshot and measured compile cost

The assembled factory owns a disposable per-Texture2D material collection. Each
texture's private GetImage snapshot and DetectAlpha fact are captured once and shared
by sprite validation and every surface asset referencing it. Pixel image is not
exposed for mutation. Snapshots are disposed after compilation; borrowed textures
are not disposed or altered. This is not a global mutable-material cache.

A complete-opacity proof avoids redundant per-triangle alpha-domain enumeration.
It never bypasses UV bounds, finite coordinates, geometry, support, or native-alpha
sampling. Matrix tests cover RGB, all-opaque RGBA, RGBA transparency, alpha 239/255,
wrong UV, geometric gaps, actual native-alpha hole, unchanged borrowed textures,
snapshot identity, and caller-image edit isolation.

Same roof geometry/material: old isolated compiler reached the 75-second timeout
(exit 124); final implementation 67.2676 ms in this cloud run. Other final component
times are 18.55–316.90 ms. This is compiler/loading cost, not frame-rate or whole-scene
performance acceptance. Raw before/after/final logs retained.

## Withdrawn attempts / invalid test observations

The half-texel-inset source is retained only as a rejected numerical workaround;
it is neither selected nor an accepted visual edit. Its interrupted positive/
equivalence attempts are not validation passes. The old negative UV resource is
retained as the guard failure.

The first migrated foot test incorrectly multiplied already projected camera
depth by 16/TileSize, producing half the actual value. Its failing log/result is
retained as invalid-depth-units evidence, not a scene regression. Final test
compares Depth(contact)+Visual.FrontOffset against the projected original source
foot, with no tolerance relaxation.

First screenshot pose (23,14) was illegal and rejected before rendering. It is not
evidence; the final display-only diagnostic uses legal (24,15).
