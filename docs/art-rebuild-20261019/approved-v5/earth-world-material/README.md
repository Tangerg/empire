# Earth material: explicit terrain and generated shore banks

## Decision and scope

The reviewed production period is **256 logical world units**. Both c01-12 and c01-02 were rendered at 128 and 256 with the same camera and frozen actors; 256 reads more quietly. The 128 views remain labeled diagnostics. The warmer, lighter native soil palette is retained.

This comparison is based on **bf1bc3ebb4632285f63801c5d7344c06e3fa4276**. It includes that checkpoint's named Laiya and generic Roderick. Later named-Roderick activation and other main-branch artwork are not part of these captures.

Source `earth-1/master.png` is the original 1254×1254 RGB image, SHA-256 `67b614fcc35c14211232dcc48da0a471aa684ae15878187a79344baf7e92623c`. The four original earth identities and order are registered in the surface catalog:

- earth-1: dry compacted soil; actual world material used by both blend consumers
- earth-2: crossing ruts; catalog-only
- earth-3: longitudinal ruts; catalog-only
- earth-4: wet/puddled earth; catalog-only

These are four native sources. The 47 technical mask states and low/high-density exports do not add painted-source counts. No terrain was reclassified to use the other three catalog slots.

## Actual consumer coverage

| Level | Explicit earth blends | Generated shore banks | Total |
|---|---:|---:|---:|
| c01-02 | 0 | 28 | 28 |
| c01-05 | 55 | 0 | 55 |
| c01-06 | 0 | 28 | 28 |
| c01-12 | 114 | 38 | 152 |
| c01-14 | 0 | 40 | 40 |
| All other 11 | 0 | 0 | 0 |
| **Total** | **169** | **134** | **303** |

The explicit-blend path derives neighbors from matching `material.blend`. The shore path keeps its original no-blend plus cardinal-water-adjacency predicate, including its original treatment of off-board neighbors. Both now use the same world material replacement policy, but retain their separate Blob47 index calculations. Blends still precede water; no water cell is painted as earth.

`consumer-roles.json` records the exact cells. `scope-comparison.json` and both contract snapshots show that only the five listed levels' ground/editor-ground SVG projections change. All 16 rule/state/under-unit/over-unit projections are identical; all scene layers of the other 11 levels are identical.

## Derivation and geometry guarantees

- The 512×512 low-density and 1024×1024 high-density RGB planes are full-canvas resizes. Sampling uses a 256×256 world period at origin [0,0], independent of tile and camera boundaries. Runtime selects the high-density plane, giving 4 raster pixels per logical world unit.
- The only pixel-level seam operation is opposite-edge averaging of the outermost 1 raster pixel. Every interior pixel equals the direct full-canvas resize. There is no crop, whole-image blur, recolor, or painted replacement texture. This does not establish that the untreated native image is naturally periodic.
- The original earth transition atlas is unchanged. The derived white masks copy **every alpha byte** at 32/64 cell sizes, preserving all 47 indices, bit order, normalization and the transparent 48th padding slot. No old transition RGB is composited back over the new material.
- Actual SVG rasterization confirms all 47 alpha shapes match the old rendering exactly at 1× and 2×. The tiled world projection matches the single continuous reference plane in every RGBA channel.
- The shared mask-reader extraction also serves the existing forest and stone consumers. Their output is unchanged, verified by all-level contracts and byte-identical forest review SVG fixtures. This does not activate any additional material family.

## Complete-scene evidence

Start with `c01-12-before-128-256.png` and `c01-02-before-128-256.png`. Each panel contains the full actual BoardView scene, with its period labeled. Individual complete views are in `before-*`, `after-*` (selected 256), and `diagnostic-128-*`.

- c01-02: 20 actors and 9 rule-building drawings; all 29 common PNG sources have equal hashes
- c01-12: 19 actors and 9 rule-building drawings; all 31 common PNG sources have equal hashes
- Standing SVG content, actor state, bindings and draw order are byte-identical before/after in each level
- Before-only raster: original `transition-meadow-earth@2x.png`
- After-only rasters: `earth-floor@2x.png` and `earth-blend-masks@2x.png`

`reproducibility.json` records source and implementation hashes. The used PNGs are frozen under `sources/`; each compressed SVG refers only to this local archive. `archived-render-verification.json` records pixel-exact reconstruction of all six images.

These are offline rasterizations of actual BoardView/SvgBoardSurface output, with nested viewports and actor facing normalized by the repository's review rasterizer. They are not live-browser or live-GPU screenshots. Backend contract tests exercise SVG/Pixi projection parity with their existing test doubles; they do not establish GPU driver behavior.

## Validation

- 22 focused TypeScript tests: earth, forest, stone and map-scene
- 15 SVG/Pixi backend contract tests, including hidden-selected-actor guards
- 37 Python tests: earth masks 6, forest masks 6, stone masks 6, surface export 11, field export 8
- Typecheck and all four app builds pass
- Architecture helper-uniqueness guard passes (one targeted test)
- All 16 scene/rule/state projections compared as described above
- All 60 replay streams and outcomes are byte-identical: 3,534,788 bytes; SHA in `replay-comparison.json`
- Earth world/mask stale checks pass; existing forest/stone mask stale checks also pass
- Full repository suite was not rerun in this sparse checkout; the integrator runs that at a fixed complete-main checkpoint

Logs are compressed under `validation/`. Native hash/interior proofs, exact mask exports and actual plane comparisons are included separately. No failed assertion was bypassed or widened for this change.

## Reproduction

Run from repository root with its existing dependencies:

```sh
npm run typecheck
npm run build:apps
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-earth-material.test.ts packages/story-candidate-01/src/presentation/candidate-01-forest-material.test.ts packages/story-candidate-01/src/presentation/candidate-01-stone-material.test.ts packages/story-candidate-01/src/presentation/candidate-01-map-scene.test.ts
npx vitest run packages/game-ui/src/art/__tests__/two-backends-agree.test.ts
python -m unittest discover -s tools/art -p 'test_earth_masks.py'
python tools/art/export_field_material.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-earth-floor-v2.json --check
python tools/art/export_material_masks.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-earth-blend-masks-v1.json --check
CONTRACT_OUTPUT=/tmp/earth-contract.json npx vite-node --config vitest.config.ts tools/art/review_candidate_scene_contract.ts
EARTH_REVIEW_OUTPUT=/tmp/earth-plane npx vite-node --config vitest.config.ts tools/art/review_earth_material.ts
SCENE_REVIEW_LEVEL=c01-12 STANDING_REVIEW_OUTPUT=/tmp/earth-c12 npx vite-node --config vitest.config.ts tools/art/review_gray_flag_scene.ts
npx vite-node tools/replay-sweep.ts > /tmp/earth-replay.txt
```

For a frozen archived view, decompress its `runtime-standing-full.svg.gz` next to the PNG and call `tools/art/render_ground_review.py:rasterize` on that directory. The SVG's relative source paths then resolve to this evidence archive. Raw Inkscape conversion alone omits required nested-viewport/facing normalization.

## Remaining visual boundaries

The inherited angular banks and narrow path bars remain angular. Existing grass tile boundaries, tall scenery, buildings and standing occlusions remain visible. This change improves material scale/continuity and removes old earth RGB at both real consumers; it does not claim to resolve map geometry, all occlusions, or the whole scene's art integration.
