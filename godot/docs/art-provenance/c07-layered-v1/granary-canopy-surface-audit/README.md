# Granary cloth canopy — shared formal-loader slice

Unbound **static** main-house-plus-canopy composition. No movement, AI, campaign commands, save changes or real gameplay. Official C07 stays painted. The original reviewed17-member cloth/wood geometry and material are preserved without new artwork, UV edits or runtime wrappers. Cargo/yard/other objects are incomplete, and roof/ground sharpness/contact-shadow debt remains.

## Ownership and reproduction

The existing `SceneSurfaceDocument` and compiler own the explicit runtime geometry/UV/contact, drawing, coverage and bounds. `TexturedSurfaceExport` transfers the archived source with the same original-member triangulation → original-triangle x-slab clipping → affine original-triangle UV interpolation as the reviewed renderer. Runtime consumes explicit per-slice contact, not the offline curve. Regenerate, never separately advance both. Source origin(380,125), scale1,125 structural contacts,474 clipped members,902 compiled triangles. Same-support batching yields125 polygon nodes instead of474 historical ones; this is a proven node reduction, **not a whole-level performance/FPS pass**.

Only west384,216 is the original visible ground join.408,219 is hidden structural support,439.5,223 and503,207 authored hidden completions, not recovered original feet. No cargo foot is attributed to the main building. All original base placements/sites are retained; appended canopy is pass-through with no site. The17members pass the formal full-texel opaque guard using the existing C04 material without threshold/coverage-mode changes.

Use `C07_TEXTURED_SOURCE=res://granary-canopy-geometry.json` and `C07_TEXTURED_OUTPUT=res://data/scenes/c01-07-granary-canopy-surface.json` with the archived exporter in the external Godot.NET probe project. Fresh re-export is byte-identical, SHA256 `255d8ea2b86e220d93235d5605d8d260798e7de1046ae88de603f4ec714ccfd2`.

## Equivalence, including a genuine rounding difference

Do not call the first strict-zero renderer comparison a pass: it failed and is preserved. It measured maximum position difference0.000030517578pixel, only18X-coordinate occurrences in support92: old471.99997/472.99997 versus formal472/473. UV, tint and depth were already exactly identical, as were12474 material coverage samples and ordered site ownership.

To identify rather than hide this difference, the frozen reference renderer now records its original clipped source polygon as test-only metadata before subtracting the contact or applying projection. This changes no drawing. The final comparison requires **formal2706 vertex coordinates to equal those original source points exactly (error0)**. The previous per-column projected transform is the side with the1ULP deviation. The test additionally bounds that observed old/new discrepancy to one coordinate ULP, not a general pixel epsilon. No production geometry or renderer was changed to imitate old rounding.

Native same-window GPU comparisons exclude only72caption rows: front/back at zoom3 each15changed pixels, zoom1.25 has6changed pixels, maximum channel difference1. Not bit-identical. Viewport1180×812. Source/raw/ULP reports and original failed-zero logs remain separate.

## Actual C07 appearance and pointer controls

- Torren east at legal27,13: actual alpha439,200 and440,204 remains behind real wood, not pickable;495other body pixels remain visible.
- Torren east27,15:439,217 and439,219 remain pickable;601body pixels visible.
- All17fixed material/void checks pass.
- Door568,162→Object35,14; approach584,232→Ground36,14; cloth438,166→Ground27,10. No extra site.
- Actual raider-1 wolf rider36,13 remains visible/pickable at579,178 and579,180 before the house stone foot.
- Pure display clones preserve canonical state and have0rendering issues. Six C06 paused/entry/source files retain prior hashes; paused SHA e8add96b2258b1e940ddecef4d6f5bda7414127562f89df47144325509e69bdf.

Two valid formal-resource negatives must reach their exact target, not merely exit1: flatten every canopy contact to the west footY216 fails actual local-depth front/back; filling the physical open bay with known opaque wood fails the fixed material/void contract. The fill uses constant clean woodUV408,450 to avoid unrelated material initialization failure. Both are retained with result JSON and error logs.

## Validation boundary

Fresh production and external builds succeed with0warnings/errors. Runtime source is unchanged from44a2fa2e; prior C01–C06 static regression applies to that identical code, not rerun as campaign actions. This batch freshly exercises formal loader/materials, source-coordinate equivalence, actual appearance/pointer controls, two deliberate negatives, deterministic export and native comparisons. No whole-scene integration or full-campaign play assertion.

Fresh SceneSurfaceChecks passes14 assertions; source metadata guard passes. Parent reviewed formal rear image and accepted preserved cloth/post shape and source-coordinate proof. Native comparison images remain labeled incomplete/static/no gameplay.
