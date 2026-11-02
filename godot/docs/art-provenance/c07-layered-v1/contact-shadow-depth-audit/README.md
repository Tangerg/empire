# Existing contact shadow depth correction — static evidence

This is a rendering bug repair, not final block art acceptance or C06/C07 gameplay.
The unique selected C07 assembly remains incomplete and runtimeReady=false; the official scene is unchanged.

## Cause and ownership
The original ellipse was a child of the first standing depth strip. Three house anchors were correct,
but their inherited depth put the shadow behind the actual floor. Native legacy vs hidden images differ
by **zero RGBA pixels**. Reusing the existing bounded PaintedSurface floor projection restores **96 pixels**
(max channel difference 47) for those three original ellipses. No texture, size, opacity, geometry, actor,
rule, or physical support changed.

AssembledGroundView now retains one hidden source per existing authored contact, preserving its texture
lifetime (cache-owned shared texture or source-owned uncached fallback), and uses the existing registered
floor projection. The projection clips only ground tops, derives depth through BoardProjection, and is
not registered in standing Visual/Surface/Pick. It is ordered after base floor at equal surface depth.
No new shadow renderer, resource schema, physical-foot model, or alpha threshold was introduced.

## Verification
- Fresh production and external probe builds: zero warnings/errors.
- Formal GPU result matches all 96 three-house repaired pixels exactly. Another 227 pixels differ from
  the three-house diagnostic because formal code also repairs other existing contacts (right courtyard
  and planters). These are intentional repairs, not whole-image equivalence.
- Selected static clone: 22 contacts; three repeated RenderState calls preserve floor/source identities,
  texture identities and 8309-node count. Hiding every decoration leaves all 5120 sampled Picks unchanged.
- C01–C06 initial-state-only rendering: all 12590 cell Pick hashes exactly match the prior static baseline.
  No actions, AI, turns or save writes were executed.
- C01 complete cached and uncached lifecycle: each 97 contacts; projection replacement frees old floor;
  repeat render preserves replacement; unloading frees sources/floors and leaves zero projection event
  subscribers. Uncached generated textures are released. A second board construction covers reload.
- Existing SceneSurfaceChecks: 60 assertions including depth/transform/material/sibling/shadow barriers
  and repeated grouping. PaintedSurfaceBroadPhaseChecks: 8129 exact geometry/UV/depth/pointer assertions.
- The selected block’s original orange chimney/doors/front, blue front/back/seam, annex front/back/side,
  left frame true holes and rooted stems all pass their existing formal-loader static contracts.
- Six paused true-state files retain their hashes. Official C07 file hash recorded separately.
- Read-only review found no blocking lifetime, batching or Pick ownership defect; clarified cache vs
  fallback ownership wording before closure.

The tiny existing ellipses are not sufficient evidence of finished contact art. Ground/source sharpness,
block-wide material integration, restrained contact shading and unfinished whole-city responsibilities
remain open. Large upper-left generic placeholder is not a new block house.

## Test governance correction
The final metadata check exposed missing .gdignore markers in the two immediately preceding audit
folders (left wall lighting and open garden). Added scoped markers, following the existing evidence-folder
contract; no runtime import/UID guard was weakened. The first failure summary is retained.
