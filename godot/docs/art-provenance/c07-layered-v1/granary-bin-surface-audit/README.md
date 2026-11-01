# Low granary grain bin — formal shared-loader migration

Static display only: actual C07 appearances in detached canonical clones, no commands/AI/saves/real-play progress. Official C07 remains painted. This adds the reviewed8-member bin to the existing **formal main-house + cloth canopy** preview; no new artwork, UV, support, rule, shadow or runtime-code change. Other stock/yard/scene objects remain incomplete.

## Owner, shape and reproduction

`c01-07-granary-bin-surface.json` uses the same shared runtime compiled surface/contact owner, no bin-specific wrapper. Archived source/exporter is reproducible offline provenance, not another runtime contact curve. Preserve source left417,251/right441,243 and actual column432,245 local contacts; hidden side completion and closed front planks remain explicitly authored design. Dark seams were not proven transparent holes and are not converted into voids. The earlier whole-sprite silhouette was rejected; its PNG supplies selected opaque wood/grain material only, never that failed silhouette.

Original member triangle → clipped source column → affine same-triangle UV ordering is unchanged. Resource origin(410,218), scale1,33 structural support columns,201clipped members/376triangles. Same-support batches use33 Polygon2D nodes rather than201 reference polygons. This is a node-count reduction, **not measured full-scene FPS acceptance**. Existing placements and ordered sites are preserved; the bin is pass-through and owns no new site.

In the external probe project, run `TexturedSurfaceExport` with `C07_TEXTURED_SOURCE=res://granary-bin-geometry.json` and `C07_TEXTURED_OUTPUT=res://data/scenes/c01-07-granary-bin-surface.json`. Re-export is byte-identical: SHA256 `8082c46e81a47e6f68db85a5648f0cc6c7b219a5b0663185992c44c00658acd6`.

## Focused verification

- Formal material guard validates the existing UVs without opacity threshold/mode changes.
-1128triangle vertices match original source clips **and** previous renderer exactly; UV/tint/depth errors0.1190material coverage samples match, sites unchanged. The reference renderer's source-polygon metadata is observation only. Unlike the prior cloth case, this object has no observed1ULP coordinate difference.
-11fixed material/void samples pass. Actual Torren east behind27,14 is correctly occluded at432,224/225; front27,15 remains pickable at both. West-facing same real appearance repeats the relation at432,228/229. All witnesses have actual opaque actor alpha. The old rejected430,224 front-alpha2 sample remains withdrawn; it is not used here.
- Existing granary door568,162→Object35,14 and approach584,232→Ground36,14 remain. State unchanged, rendering issues0.
- The formal-resource negative flattens all contacts to left footY251 without changing material/shape. It exits1 specifically at `Granary bin actual-role sloping-depth contract failed`; not an initialization or unrelated material error.
- Same native viewport1180×812,72caption rows excluded: previous→formal differs12(back)/11(front) pixels at zoom4,5pixels at zoom1.25, maximum channel1. Not bit-identical. All images explicitly label static/incomplete/not gameplay.
- Fresh production/external builds0warnings/errors. Fresh SceneSurfaceChecks14 assertions pass. Runtime code remains identical to44a2fa2e, whose C01–C06 pure static regression remains applicable; no action-bearing campaign tests are rerun.
- Six C06 paused/source/entry files retain prior hashes; paused SHA e8add96b2258b1e940ddecef4d6f5bda7414127562f89df47144325509e69bdf.

## Boundaries

Black wood seam still misleadingly resembles an opening, grain remains bright/fine, contact shadows and ground/style unification remain outstanding. This migration preserves the reviewed geometric candidate; it is not final art, full stock/footprint or full C07 acceptance. Native same-camera source/reference/formal evidence and exact negative stage are retained rather than replacing historical failure evidence.

Parent reviewed formal rear image and accepted preservation of the bounded candidate. Indexed metadata guard passes; official55-group painted scene is not switched.
