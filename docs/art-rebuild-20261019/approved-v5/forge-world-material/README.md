# Forge stone: original four slots and the actual world-material consumer

## Result

The existing `c01.forge` tone-zero ground now uses the approved full source `forge-stone-1` at a 256 × 256 logical world period. This replaces 252 legacy tile stamps in c01-14 only. The other15 candidate levels have identical ground/editor-ground output; all16 rule/state and standing-layer projections remain identical. No deployment, navigation, mission trigger, rule structure, actor, scenery, hidden-state handling or foreground geometry changes.

All four original `surface-forge-stone` catalog slots are exported from their exact approved source identities at32/64 pixels per slot. Slots1–3 remain catalog-only; there is no new tone distribution or terrain consumer. The existing atlas manifest, original order and four-subject count are retained. Derived atlas/world densities do not count as new artwork subjects.

## Actual complete c01-14 comparison

`before-after-labeled.png` and the separate complete frames compare the same f31930c baseline, including the newly registered Laiya appearance, 22 actors, 8 rule-building drawings and 852 × 648 camera. All32 common source PNGs match exactly. Only the old forge atlas source disappears from the actual ground and the new world-material PNG appears. The entire actual standing SVG layer is byte-identical, not merely the actor count.

At256, principal stones often span roughly40–80 logical units, comparable to or larger than a figure's body. The dark low-contrast planes remain behind the brighter chunky figures and forge props in the complete scene; parent visually accepted256 on2026-10-19. No recoloring or blurring was used to manufacture that result. The dense existing kilns, coal piles, horizontal stone structures and hard boundaries between terrain materials remain. This is ground-material integration, not a claim of whole-level visual completion.

Evidence is the actual BoardView/SvgBoardSurface rasterized offline with the existing nested-SVG/facing normalizer. It is not a live GPU/browser capture. Both archived SVGs reproduce every RGBA pixel of the stored complete PNGs.

## Native source identity and derivation

The source prefix is `packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/surface-forge-stone/`. Each `forge-stone-{1..4}/master.png` is native1254² RGB, byte-identical to the fixed baseline Git source. Exact SHA-256 values are in `native-sources.json` and `source-and-interior-verification.json`.

- Slot0/source1: cool dark charcoal polygon paving, active world consumer, SHA256 `025bfaa1c9e17b7ea83f44851dad28bf791b0ed273d78f12864d7538b3294f9d`
- Slot1/source2: warmer brown-gray cobbles, catalog-only
- Slot2/source3: dusty brown exposed ground with interrupted rectangular paving, catalog-only
- Slot3/source4: smoother broad dark-gray paving, catalog-only

Source0 mean RGB is about53/53/55. The other sources preserve their authored warmer or more neutral palettes; original palette matching is not asserted exact. All native canvases stay untouched and opaque. Full native source/QA remains in the masters directory; no source foot, obstruction or gameplay footprint is invented for continuous ground.

The unchanged field exporter applies full-canvas LANCZOS resize to512² and1024² at a fixed256logical period, giving2 and4 raster pixels per world unit. Only the already approved opposing outermost one-pixel seam bands are averaged. Every interior pixel equals the direct resize, verified at both densities. No broad filter, recolor, crop or paint is applied. Native periodic edges are not naturally identical; one-pixel matching does not establish organic seamlessness across all stone shapes.

The existing world-tile projector retains zero phase, uncropped fit and technical half-pixel coverage. The actual 32-pixel tiled projection equals one continuous plane in every RGBA channel at1× and2× (`plane/comparison.json`). Forge terrain has no old road/edge RGB overlay; independent coal/kiln scenery remains untouched.

## Verification

- 21/21 focused TypeScript tests passed across forge, graveyard, stone, world projection and map scene
- 9/9 surface-export and8/8 field-export Python tests passed
- Typecheck and all4 application builds passed
- All60 replay event streams/outcomes are byte-identical
- All16 scene contracts differ only in c01-14 ground/editor-ground
- Exact source bytes, mode/dimensions, original four-slot order, existing catalog families, unchanged rule map,252 real tone0 pieces, camera phase and editor behavior are tested
- Actual tiled-vs-plane RGBA equality at1×/2×; immutable full-SVG source archive and exact complete-frame rerenders retained

One additional architecture guard was run and fails on the known unchanged baseline duplicate `hash` declaration in `review_candidate_scene_contract.ts` and `review_hero_appearance_contract.ts`. Its full log is retained; the parent is fixing that separate main issue. No new forge helper duplicate is reported. The world-plane diagnostic now has one shared implementation used by the existing graveyard and new forge entry points; both prior SVG outputs were checked byte-identical. No architecture assertion was removed, weakened or bypassed.

The full repository suite was not rerun in this sparse worktree. This is focused validation plus fixed replay/scene proof, with integrated-main testing still separate.

## Reproduce

```sh
python tools/art/export_field_material.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-forge-base-v2.json --check
python tools/art/export_surface_overrides.py --check
python -m unittest discover -s tools/art -p test_surface_overrides.py
python -m unittest discover -s tools/art -p test_field_material.py
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-{forge-material,graveyard-material,stone-material,world-material-tile,map-scene}.test.ts --maxWorkers=1 --minWorkers=1
npm run typecheck
npm run build:apps
npm run --silent sweep > /tmp/forge-replay.txt
CONTRACT_OUTPUT=/tmp/forge-contract.json npx vite-node tools/art/review_candidate_scene_contract.ts
SCENE_REVIEW_LEVEL=c01-14 STANDING_REVIEW_OUTPUT=/tmp/forge-scene npx vite-node tools/art/review_gray_flag_scene.ts
python tools/art/render_ground_review.py /tmp/forge-scene --scene-only
FORGE_REVIEW_OUTPUT=/tmp/forge-plane npx vite-node tools/art/review_forge_material.ts
python tools/art/render_ground_review.py /tmp/forge-plane --scene-only
```

The complete catalog check requires all registered native masters. Development used a temporary catalog containing only the same four forge entries to write/check only this atlas; no unrelated atlas output changed. Baseline capture and rasterization completed before either atlas was replaced, preventing accidental comparison with the new catalog under old code.

For portable frozen rendering, decompress either `runtime-standing-full.svg.gz` beside its PNG and call `render_ground_review.rasterize(directory,(1,))`. The relative hrefs resolve to the local `sources/` archive. Only source URLs were relocated from captured SVGs. `reproducibility.json` retains source hashes, original/relocated SVG hashes and implementation hashes. `scope-comparison.json`, `replay-comparison.json`, export reports and compressed logs record exact measured outcomes.
