# C03 opening farmland terrain sources

## Scope and identity

Four independently authored native source candidates for parent selection and integration. This worker only authored new `masters/approved-v5/canonical-terrain/<hash>/` folders and this review directory. No shared catalog, code, runtime, manifest, consumer or Git changes. Native32 only; enlarged review images are nearest-neighbor diagnostics, not HD assets.

The full original `terrain-board-01.png` was inspected before generation. `docs/story-candidates/candidate-03/art-assets/07-topic-catalog.md` lists these terrain identities at zero-based positions 0–3. `packages/story-candidate-03/assets/final-ancient-china-v1/tools/build_final_ancient_china_v1.py` reads the four-column/two-row boards row-major, so these are top-row columns 1–4. Its connected-index set includes 3 but excludes 0, 1 and 2. Original native32 runtime strips were also inspected, and their exact bytes were preserved in `original-reference/`.

- `c03-terrain-99390ac8`, 龟裂灾田: parched ochre clay plates, dark fissures and sparse straw-yellow grass. Four material variants.
- `c03-terrain-980abbed`, 退水烂泥: dark mud masses left in shallow blue-gray retreating floodwater, with sparse reeds. Four material variants.
- `c03-terrain-b499c3bf`, 复耕田垄: diagonal cultivated earth ridges and young green crops. Four material variants.
- `c03-terrain-c053bd0c`, 粮车与难民车辙路: ground-level compacted-earth wheel ruts. One full-square material for all sixteen historical NESW masks. The original illustration contains a grain wagon, horse and refugee tents as narrative context. The canonical label and connected terrain contract concern the road and wheel traces, not a miniature wagon/unit. The new source deliberately retains wheel-rut ground identity rather than duplicating those objects in every connector.

No completed identity or existing source folder was overwritten. The hash-only directory convention follows `update_approved_ledger.py`. The historical builder's upstream `packages/story-candidate-03/assets/manifest-complete.json` is absent in this checkout, so its contents were not claimed as inspected. The available canonical topic catalog, builder, canonical manifest entries and actual runtime strips establish the mapping and contract.

## Generation and exact preservation

Exactly four built-in `image_gen.imagegen` invocations, one per topic, with no CLI/API fallback and no regeneration. Each output is an opaque RGB PNG at 1254×1254. Every `raw.png` and `master.png` is byte-identical to the returned generation file, verified by bytes and SHA-256. The generated file paths, requests, complete prompts, reference hashes and source hashes are recorded alongside each source, in `reservation.json` and in `source-inventory.json`.

The entire old atlas was subject evidence only. The corrected Uker memory-overlay and Soler heated-walkway broad-matte-v2 sources were viewed and supplied as surface-simplification references only, not identity or palette references. Prompts request Legends of Kingdom Rush-inspired exaggerated chunky matte planes, prohibit granular mottling/painterly microtexture, and favor native32 readability. All four complete generated sources were inspected. Parent also independently inspected the complete sources and accepted the broad planes, including the road's disclosed sparse secondary patches, without requesting a correction.

No source was cropped, recolored, repainted, blurred or resampled after generation. Three sources have exact equal 627×627 quadrant registrations in TL/TR/BL/BR order; the road uses the complete 1254×1254 canvas. Derivation uses the existing `read_material`/`render_material` helpers: native32 Lanczos sampling, the established outermost-one-pixel opposite-edge average, original alpha masks and transparent RGB cleanup. This documented runtime derivation does not alter the native source files.

## Visual review

Viewed all complete generated sources, `all-native32.png`, `all-zoom2.png`, `original-strips-native32.png`, and all four actual-adjacency connected-grid diagnostics on black, white and olive backgrounds. The connected grid derives NESW bits from its occupancy, rather than arbitrarily tiling mask numbers.

- Drought field: large irregular plates and dark cracks remain readable at native32. Straw tufts reduce to small warm accents. The source generated two tufts per quadrant rather than the requested one and uses subtle broad gradients plus thin edge highlights; it is clean, not granular or mottled. Some small peripheral clay fragments remain, but no painted microtexture. Four different crack layouts are evident. The grid repeats recognizable crack clusters and has some square-boundary motif interruptions.
- Retreating mud: large brown masses and blue-gray pools remain very clear. At native32 the pools read more like connected blue channels between mud islands than isolated puddles. The single reed per quadrant survives only as a tiny olive accent. Source mud tops are broad and smooth, with extra rim shading beyond the strict two-plane target. Repeated three-island arrangements make the grid visibly patterned, and some island silhouettes end abruptly at cell joins despite exact outer-edge equality.
- Reclaimed furrows: strong ochre/dark-brown diagonal rows and green seedlings are immediately legible. Four arrangements share scale and diagonal orientation. Some leaves have more than one shade division and cast shadows, but no fine soil grain. Native32 loses leaf-specific shapes while preserving planted-row identity. Regular seedling repetition and interior diagonal alignment discontinuities are visible in the grid; exact one-pixel edge equality is not a claim of fully continuous furrows across all variant pairings.
- Wagon/refugee rutted road: paired grooves remain visible within the inherited narrow cardinal strips and central core. No wagon, horse, tent or character was generated. The source has several broad shade bands around the ruts, subtle whole-plane gradients and sparse small secondary dirt patches. These are disclosed rather than retouched; they are not dense granular mottling. Most peripheral patches are clipped by the historical mask. The connected grid is coherent but necessarily regular and ladder-like under the narrow inherited topology. Road semantics are clearer than the original masked wagon fragments, but native32 cannot retain all full-source groove detail.

## Mechanical results and integration handoff

- Four independent, opaque, square sources with unique SHA-256 values; returned/raw/master equality passes for all four.
- Exact equal quadrant windows for three variant sources and the full square for the road.
- Four native32 plan outputs only; no 64px/HD output.
- Original alpha strips match byte-for-byte for every topic.
- Derived/distinct cell counts are 4 / 4 / 4 / 16.
- Exact allowed cardinal-edge equality passes: 16 east–west and 16 south–north pairs for each variant strip; 64 and 64 for the NESW road strip.
- Read-only export planning passed against the real tree and isolated `proposed-registration.json`.
- Four isolated exporter writes and all four `export(..., check=True)` checks passed inside `export-fixture/`. Nothing was exported to the shared runtime tree.

`checks.json`, `native-review.json`, `export-plan.json`, `isolated-export.json` and `isolated-export-check.json` contain exact evidence. `proposed-registration.json` is the merge-ready four-entry proposal, not a live catalog. `build_review.py` reproduces the diagnostics and the private exporter fixture. It must not be pointed at a different output root or replaced with a shared runtime export without the parent's integration decision.

These are mechanically valid, visually inspected source candidates, not a claim of live game integration or final scene acceptance. Parent owns the catalog merge, formal export/binding, combined pack/scene review and Git checkpoint. Preserve the disclosed repetition, join and narrow-mask limitations in the handoff. No gameplay consumer or HD deliverable was created.

## Integration-owner acceptance

The integration owner independently inspected all four full1254 masters and the complete native strips on black, white and olive backgrounds. Broad matte plane treatment is accepted; sparse road dirt patches do not rise to the earlier Soler mottling defect. Four sources are selected in the existing canonical terrain owner and exported/check-verified into the original native32 catalog. No camera, engine or C03 scene placement changes are part of this art packet. Full connected-scene acceptance remains separate, especially the furrow alignment discontinuity and ladder-like road repetition. The reproducible private export-fixture directory is intentionally ignored rather than committing duplicate source copies; its JSON verification reports remain tracked.
