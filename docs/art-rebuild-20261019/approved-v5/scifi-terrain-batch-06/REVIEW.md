# C02 Soler terrain: four native-reviewed source candidates

Generated 2026-10-20 with four separate built-in image_gen.imagegen calls. This directory and the four matching canonical-terrain source directories are the only authored deliverables. No shared exporter, catalog, runtime, manifest, progress or Git changes were made. No CLI fallback, HD output or new gameplay consumers.

## Original subject provenance

Canonical manifest order after Nereia is soler-blue-ice, soler-brittle-ice, soler-deep-factory, soler-heated-walkway. The original complete terrain-board-01.png was visually inspected. The original topic catalog at docs/story-candidates/candidate-02/art-assets/07-topic-catalog.md lists blue-ice, brittle-ice, heated-walkway, deep-factory at indices 4–7. The builder packages/story-candidate-02/assets/final-scifi-v1/tools/build_final_scifi_v1.py lines 264–275 consumes four-column boards row-major, establishing bottom-row columns 1, 2, 4, 3 for this canonical manifest order. Original native strips independently corroborate these subjects. The original manifest-complete.json input is absent from this checkout; that missing input is not claimed inspected.

Original atlas and native strips are copied unchanged into original-reference. Every new material has the exact prompt.txt, request.json, generation-result.json and provenance.json, including hashed reference paths. The original atlas was subject reference only, and the approved Farlight cargo material was style reference only.

## Source preservation and sampling

Each independent generator output is an opaque RGB 1254×1254 PNG, copied byte-for-byte as raw.png and master.png. No source cleanup, repainting, color repair, fitting, scaling or hidden cropping was performed. proposed-registration.json records hashes and exact square windows. Blue ice uses four equal 627×627 quadrants in TL, TR, BL, BR order; the three NESW materials use the full 1254×1254 square.

build_review.py imports existing read_material/render_material helpers without changing their registration whitelist or shared code. It uses established Lanczos native32 sampling, historical alpha masks, and one-pixel opposite-edge matching. All historical alpha bytes match the current original runtime strips exactly. Distinct cell counts are 4/16/16/16. Exact RGBA edge checks pass all 16 east-west and 16 south-north pairs for blue ice, and all 64 plus 64 allowed pairs for each connected topic. These checks establish only outermost-edge equality, not perfect continuation of interior motifs.

## Visual review and limitations

The full original atlas, original native strips, all four complete source masters, all-zoom2.png and all four connected-grid-native32.png diagnostics were visually inspected. Connected previews derive NESW indices from actual adjacency and show black, white and olive backgrounds. Enlargement files are nearest-neighbor diagnostics, not HD runtime assets.

- Blue ice: four distinct blue slab variants retain saturated cobalt crevices and pale chunky tops. Broad ice shapes read at native32. The generator kept more faceted surface patches and beveled relief than the flat, handful-of-shapes request; these mostly merge at native size, but a slight raised-block appearance remains. Repeated variant placement and tile transitions are visible in larger grids.
- Brittle ice: visibly paler cyan fractured plates distinguish it from solid blue ice; major branching fissures survive native sampling. Source has secondary facet patches and hairline fractures beyond the requested simplification. Native connector masks crop the plates into narrow bars, so this remains a connected brittle-ice material rather than an opaque ice field.
- Deep factory: dark navy plating and the centered ventilation opening preserve the original factory identity. The grille compresses into a nearly black central inset at native32, while blue seams remain discernible. Contrast is low on black backgrounds, consistent with its dark material, not an accessibility treatment. Symmetric central insets repeat visibly in connected grids.
- Heated walkway: warm charcoal plates and orange heating strips remain distinct from the cold factory. Cardinal strip placement survives directional connector masks, unlike many diagonal original accents. The isolated zero-neighbor mask has very little orange because the central plate is unlit; connected forms retain prominent orange strips. No heating animation or gameplay effects are implied. The repeated central junction shape remains obvious.

All three connected subjects retain the inherited narrow cross/capsule alpha silhouette unchanged. It is conspicuous in diagnostics and was neither redesigned nor concealed. Sources are matte and readable but not literally restricted to two flat colors or entirely free of texture.

Recommendation: accept all four as native-reviewed source candidates. Formal registration, runtime export, whole-pack validation, and consumer/runtime review belong to the parent and are not claimed completed here.
