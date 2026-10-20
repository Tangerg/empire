# C02 Verdant terrain sources

## Scope and identity

Four source candidates, not live registration or final style approval. Only canonical-terrain/verdant-* and this review directory are authored. No catalog, shared code, runtime, manifest, consumer or Git changes. Native32 only; larger review images are nearest-neighbor diagnostics, not invented HD assets.

Before reservation no approved/raw Verdant source directories existed. Original complete terrain-board-02.png and the original native32 strips were inspected. The canonical topic catalog orders Verdant at zero-based indices 12–15: living bridge, fungus bed, spore fog, root nest. build_final_scifi_v1.py traverses each 4-column, 2-row atlas row-major, so these are board-02 bottom-row columns 1–4. The builder's connected set contains indices 12 and 14, not 13 or 15. Original identities are pale paved bridges clasped by olive roots over teal water; cultivated purple/ochre fungus beds with cyan irrigation; ochre earth with purple mushroom groups and lavender fog; and dark plum intertwined roots, violet bulbs and cyan symbiotic insets.

The complete board-04 was also opened during identity investigation but is not a Verdant reference. The original manifest-complete.json was not inspected. Exact canonical manifest entries and runtime strip hashes are recorded here.

## Generation and preservation

Exactly one built-in image_gen.imagegen invocation per source, four total, no CLI fallback. The original board is subject evidence only. Corrected Uker memory-overlay and Soler heated-walkway broad-matte-v2 sources are surface simplification references, not the original mottled versions. Prompts explicitly prohibit secondary painted patches, grain, small facets and fine detail.

All four outputs are opaque RGB 1254×1254 PNGs. Raw and master files are exact returned bytes, verified against the tool output file as well as one another. Equal quadrants TL/TR/BL/BR are used for fungus bed and root nest; full square for living bridge and spore fog. No post-generation repainting, recoloring, cropping or source resampling. Existing isolated read_material/render_material helpers provide native32 Lanczos samples, historical masks and the established one-pixel edge averaging. Exact allowed cardinal-edge equality does not imply visually seamless interior motifs.

## Visual review

Inspected all four complete generated sources, all-native32.png, all-zoom2.png and all four native32 connected grids on black, white and olive backgrounds. Grid masks derive from actual cardinal adjacency, not arbitrary index tiling.

- Fungus bed: broad purple and ochre caps, olive root boundaries and cyan irrigation remain immediately readable. Four distinct cap arrangements survive. Caps are smooth and clean; roots have more highlight lobes and secondary branches than the strictest two-tone target. Soil has mild broad shading. Tiling looks deliberately cultivated and repetitive, with repeated round cap clusters and irrigation dots. The original fungus identity is maintained, but overhead caps can also read as bulbs/discs without visible stems.
- Living bridge: clean pale paving and olive root edges read as connected bridge paths. Main paving planes are quiet; peripheral roots retain some extra lobed highlights, and water has broad decorative bands. Four cyan insets were generated instead of the requested single central inset. Historical narrow masks clip most roots, water and cyan nodes, leaving primarily pale landings and slim root-edged walkways. Connected grids are coherent but regular, and native32 loses much of the full-source organic complexity. This is not a full bridge scene consumer.
- Root nest: violet bulb, dark plum roots and cyan node are distinct from fungus beds. Three-root swirls remain readable at native32; full-source roots have broad shadows plus a few small folds around the cyan node. No detailed grain or speckling. Subtle overall gradients mean the fills are not mathematically flat. Four arrangements are distinct but somewhat rotationally similar. Repeated diagonal roots do not continue naturally across every tile interior, despite exact boundary pixels.
- Spore fog: large clean lavender fog banks and ochre ground preserve the original palette and fog subject, with no particulate noise. More cloud lobes and shade bands than the requested three banks were generated. The central area is mostly bare ochre rather than the requested central overlapping fog; mushrooms sit off-center and are mostly clipped out by the inherited narrow masks. Native output reads as an ochre/lavender fog-pattern connector rather than obvious mushrooms. Connected grids are high-contrast and repetitive, with abrupt interior motif changes at some joins even though edge pixels match.

## Mechanical results and handoff

All raw/master/returned byte equality checks pass. Each source is opaque, square and independently generated. Fungus-bed/root-nest windows are exactly four equal 627×627 quadrants; living-bridge/spore-fog use the complete 1254×1254 square. All four native alpha strips exactly match original runtime alpha bytes. Distinct derived cells are 4 / 16 / 4 / 16. Exact allowed cardinal edges pass 16 east-west plus 16 south-north pairs for each variant strip and 64 plus 64 pairs for each NESW strip. Detailed pixel, source, prompt, request, alpha and reference hashes are in checks.json, native-review.json, proposed-registration.json and each source provenance.json.

Mechanically valid, visually inspected candidates. Parent review must weigh the disclosed root highlight complexity and loss of spore/mushroom identity under narrow NESW masking. No additional generation or concealed cleanup was performed. Do not equate counts or exact edges with final visual acceptance, live integration or complete terrain coverage. Parent owns source selection, formal registration, export and whole-pack/scene validation. No gameplay consumer or HD deliverable was created.
