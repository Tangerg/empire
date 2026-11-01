# Formal surface loader: first static fence slice

## Implemented boundary

The existing `AssembledGroundDocument`/`AssembledGroundView` factory now accepts resource-local UV geometry. Production `c01-07.json` is unchanged. The new `c01-07-fence-surface-preview.json` is explicitly runtimeReady=false and contains only the isolated fence plus legacy map placeholders. This is not C07 integration, gameplay or final art acceptance.

Runtime `supports[].frontPixels` is the only current contact owner. The frozen study geometry and the historical support curve are provenance inputs to the deterministic `FenceSurfaceExport`, not an alternate runtime contact source. Re-export and verify hashes rather than hand-editing generated surface JSON and its source independently. The exporter preserves the old order: clip each column, then clamp UV at resulting vertices. Runtime does not reslice or reinterpret the curve.

Each shared support group compiles its member polygons once into triangles/UV. The same private result feeds Polygon2D, native-alpha/opaque coverage, bounds, pointer clipping and depth. Site ownership remains one placement per canonical site; it is not duplicated per triangle. Geometry is exclusive with sprite front/DepthStrips. Existing sprite assets keep their complete-canvas contract. Samplers are view-owned, one per used asset; compilation allocates no Nodes. Surface nodes join their owner before further children can fail.

Opaque and native-alpha are explicit different material contracts. Opaque UV validation covers touched texels, including clamped degenerate lines and narrow triangles; native-alpha delegates actual coverage to Godot's native sampler. Tint alpha is restricted to one. Exact duplicate triangle geometry cannot evade validation by changing support, UV or tint. General intentional face overlap is not globally forbidden: replacement ownership still needs scoped controls. Physical support must lie on owned geometry, but that check alone is not a source-alpha foot proof; original physical/structural evidence remains necessary. The fence's between-foot support spans are explicitly structural.

## Evidence and acceptance boundaries

- Fresh production build and external probe build: 0 warnings/errors.
- Production SceneSurfaceChecks: 11 assertions, including opaque degenerate-line/narrow-triangle holes, true opaque line, duplicate geometry with changed UV/support, invalid physical support, native-alpha hole/pointer, scale/contact/bounds.
- Original vs formal loader: 45 support entries; 846 triangle vertices; position, UV and depth maximum errors exactly zero after normalizing cyclic triangle vertex order; 2961 source-pixel coverage samples, zero mismatches.
- Native GUI before/after, actual Torren rear (60,12) and front (62,14), zoom4: excluding the intentionally different 72px caption, each full frame differs at only 3 pixels with maximum channel delta1. This is preserved appearance, not a claimed visual redesign. Same-camera PNGs retained.
- Static positive and wrong material, duplicate primitive, early support and late support controls are tracked by their exact target assertion; fixture initialization failure is never a valid negative control.
- Performance is NOT passed: support entries remain45, but this first compiler emits282 Polygon2D triangle nodes versus144 old polygon nodes. Batch only same-depth/material geometry later; do not collapse support/alpha to improve counts. No FPS conclusion from captures.

## Withdrawn or excluded runs

The first formal probe stopped at a JSON-required sprite-front field, before any material assertions. It is not a valid control. DTO/runtime contracts were corrected and the probe rerun.

The first equivalence diagnostic compared raw cyclic triangle vertex order and reported 28.01785 position /280.02856 UV differences despite unchanged material coverage. That comparison was invalid: triangle-to-triangle topology and vertex/UV association must be compared independent of cyclic starting vertex. Its failed result/log are preserved; corrected per-triangle ordering yields exact zero, without changing geometry or tolerance.

An initial broad regression selection incorrectly included WhiteRiverChecks and LuojiaChecks, whose recursive preconditions execute independent C06/C07 no-AI action fixtures. These are excluded from this batch's authorized rendering acceptance and from gameplay claims. Luojia session was stopped with exit130; those logs are preserved, not rerun. The current/entry/source C06 JSON files all match private backup v53 byte-for-byte; the paused state remains SHA256 `e8add96b2258b1e940ddecef4d6f5bda7414127562f89df47144325509e69bdf`. Subsequent checks use explicit initial-state CreateState/Render only, never actions, AI, saves or campaign advancement. Test selection must inspect entry and recursive preconditions, not infer scope from names.

The normal C06/C07 gameplay approval remains pending. This batch neither resumes the paused game nor substitutes an artificial route for it. Final per-run logs/results accompany this audit; absent or incomplete stages must not be inferred as passed.

## Source metadata boundary
The existing metadata checker incorrectly required UID companions for 32 archived scripts beneath tracked `.gdignore` directories. It now follows actual indexed ancestor ignore markers, never a hard-coded docs exemption. Fourteen isolated positive/negative cases include nested ignored files, active outside files, removed markers and untracked markers. New runtime script UIDs come from the normal Godot editor resource scan. Archived source is retained, not renamed or deleted to hide compile/import issues.
