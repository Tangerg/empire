# Twin Hills: bounded road continuity and sampling refinement

Baseline: phase-one layout checkpoint `28d03f8`. This changes only the normal-play road presentation in `c01-01`.

## Review result

- [Complete before](before/full-scene-labeled.png) and [complete after](after/full-scene-labeled.png) use the same 1012×680 camera, field origin (74,62), actual 19 actors, four rule buildings, two farmers, scenery and standing order. Headers sit outside the full frame
- Repeated 32px earth stamps and internal apron shoulders are replaced with one continuous world-coordinate sample. Adjacent broad road cells retain solid interiors; exposed bends are rounded
- The route still follows an axis-aligned, stair-stepped silhouette. This checkpoint improves material continuity and boundary rounding; it does not establish fully natural path geometry
- Actor and building clearances from phase one are retained. No foreground was hidden and no grass/noise was painted over the route to disguise its cells

These are actual production `BoardView` / `SvgBoardSurface` snapshots, rasterized offline with Inkscape. The normal CSS actor-facing transform and nested SVG viewport clips were made explicit for that renderer. No live browser or GPU-pixel verification is claimed. Full SVGs and native/2× PNGs are retained beside labeled boards. Four same-camera native/2× detail pairs cover the west apron, village bend, southern route and east apron; exact crop coordinates are in `review-crops.json`.

## Exact coverage and material contract

The unchanged square4 map contains **80 road cells plus four settlement cells**. Paint ownership is derived from the existing road-material binding; route connectivity is derived from the catalog's route tags. No parallel path graph or authored paint-cell list is introduced.

- 32 logical pixels per cell; only exposed edges inset 5px, leaving a 22px opaque single-lane core and 54px two-cell apron
- Exposed convex corners use radius 9px; missing-diagonal concave corners use radius 5px. An analytic bounded contour implements the approved contract's intent, rather than claiming pixel equality with its initial erosion/opening proposal
- All 256 neighbor cases preserve radius-8 center coverage and the 22px core of every real cardinal connection. Cardinal neighbors, diagonal contact and absent neighbors remain distinct. Complete 2×2 apron interiors remain opaque
- The AA shoulder is source alpha only, resolved on the 2× mask's 0.5-logical-pixel lattice. No painted grass/color edge or opaque interior seam is added
- **Deliberate visual overhang: zero.** Every piece is clipped inside the union of actual painted cells. The plain village notch `(11,8)/(12,8)`, hills, forest and bridge deck receive no road paint. Bridges can supply a connection port but retain their own material
- West boundary ports at rows 9–10 and east ports at rows 6–7 reach the actual board edge. A clamped virtual neighbor preserves these ports without creating exterior paint
- One 1024×1024-logical-unit material period, world origin `(0,0)`, spans the complete 864×544 field. The source is the unchanged approved 1254² earth master, SHA256 `9afeebdb434aed8fe30459db2dbf457173c3b0d49fad2a966dbb8837718dbd53`
- Independent uncropped Lanczos exports are 512² and 1024². Runtime uses the 1024² export: **one raster pixel per logical world unit**, below native 2× world detail. There is no painted texture, crop, recolor or edge averaging

`road-visual-contract.md` preserves the design brief. `geometry-export-tests.json` records the implemented geometry, export hashes and independently checked guarantees. The legacy road atlases are unchanged and remain active for all other levels and for editor previews.

## Evidence and verification

- `render-invariants.json` and the before/after `render-contract.json` compare all 16 shipped candidate levels: level/state hashes, all standing layers and all editor ground layers are identical. The sole changed projection hash is normal-play `c01-01` ground
- Before/after `runtime-scene-provenance.json` pin source hashes and actual actors, facings, origins, contact points and standing order. All **25 common raster sources have identical hashes**. The frame stops referencing the old road/edge atlas pair and starts referencing the new earth/mask pair. No concurrent main-branch art changes are included in this comparison
- Full-frame comparison changes 42,325 native pixels. Of these, 1,383 lie in the one-pixel boundary band because the old atlas had a 0.5px per-cell bleed (`x=y=-0.5`, `width=height=33`). That legacy overhang is removed. No pixels beyond that boundary band change
- `actual-road-render-coverage.json` checks the isolated, actually emitted runtime road SVG: maximum alpha outside all 84 occupied cells is zero at both 1× and 2×, every cell center is fully opaque, and all 63,656 fully opaque native pixels match the world-coordinate earth RGB exactly
- `geometry-export-tests.json`: 16 Python road tests; 12 existing field/water tests; export `--check`; Python compilation. Exhaustive rotation/center/port/topology/coverage checks, 1,024 valid shared-edge pairs, L/T/+/ring fixtures, actual 84-cell map, independent integration and mutation checks all pass
- Shared geometric boundaries and 2× raster edges agree exactly for all 1,024 valid pairs. At 1×, adjacent pixel *areas* can differ by up to 8/255 at tangencies in 768 pairs; they are not samples of the same area. The opaque core remains intact. Runtime uses the 2× mask
- `replay-comparison.json`: independently rerun all 60 shipped replay/profile combinations. Entire event/output stream is byte-identical to phase one: SHA256 `3ebce01a551fe642db90380e9fb774f0682c1d3a546e8ba81bb8fcab4d90e0d9`, 3,534,788 bytes. Navigation, objectives, deployment, difficulty and deterministic outcomes are unchanged by this presentation step
- Typecheck and final full-suite results are recorded in `verification.json`. The sparse worktree used temporary read-only links for omitted supporting art/docs while main art continued advancing; this full run is engineering validation against those evolving support fixtures, not an immutable self-contained branch snapshot. Captured before/after runtime PNGs are regular local files with verified identical shared hashes. The integrator must run the complete suite again at its fixed main checkpoint

The backend definition-safety test now asserts every scene and actual URL-reference coverage instead of a minimum fragmented-piece count. All reference-resolution assertions remain. New tests cover actual editor fallback, all scene profiles, both campaign-choice states, material source/density, map-driven masks, bridge ownership, UV/chunk behavior and definition isolation.

An initial full run passed 914/915 tests and caught duplicated water/road binding helpers. The final implementation gives these facts one owner in `candidate-01-world-material.ts`; water output hashes remain identical. No tests or timeouts were disabled to pass the rerun.

## Reproduce

From the repository root:

```sh
npm run typecheck
npm test -- --maxWorkers=1 --minWorkers=1
python -m unittest discover -s tools/art -p 'test_road_area.py' -v
PYTHONPATH=tools/art python -m unittest test_field_material test_water_material -v
python tools/art/export_road_material.py --check
npm run --silent sweep > /tmp/road-replay.txt
CONTRACT_OUTPUT=/tmp/road-contract.json npx vite-node tools/art/review_candidate_scene_contract.ts
STANDING_REVIEW_OUTPUT=/tmp/road-scene npx vite-node tools/art/review_gray_flag_scene.ts
```

For a layout-isolated comparison, use baseline `28d03f8` and the exact art hashes recorded here, not newer main-branch artwork. The baseline scene-contract snapshot was captured before edits with the same pure projection harness now saved as `review_candidate_scene_contract.ts`. The evidence's scene readiness flag remains unchanged; this review does not promote incomplete production art.
