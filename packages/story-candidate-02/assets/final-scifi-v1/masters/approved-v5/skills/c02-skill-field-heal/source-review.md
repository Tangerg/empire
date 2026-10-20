# c02-skill-field-heal source review

## Identity and evidence

Canonical label: 战地治疗. Category skill, actual runtime 48×48, center anchor [24,24]. No HD entry or animation sequence is introduced.

The actual runtime icon was visually inspected before mapping. The matching original higher-resolution slot is skills-board-01, row 2 column 4 (one-based), exact 5px-inset crop [945, 319, 1249, 622]. The full original board, exact crop and original runtime are retained. Topic-catalog order independently agrees; alphabetical final-manifest order was not used. No label/pixel conflict was found. ART-DIRECTION and runtime export rules were read. Input/source hashes are retained.

Original r2c4 gloved hand, diagonal green injector, medical plus and green healing arcs are retained. Four original-like round action nodes remain in the output despite the prompt suggesting two or three; this is consistent with the original motif and does not change skill meaning. No red cross, text or medical brand is introduced.

## Generation and native-size review

One built-in image_gen call, transparent_background=true. Output is 1254×1254 RGBA. master.png, raw.png and raw-native-v1.png are byte-identical. No source pixel painting, recoloring, cropping, chroma removal, alpha cleanup or sharpening was performed. There are no rejected generation attempts.

Native source and source 512 composites were inspected. Style uses bold contours, large simplified matte planes and bounded functional action glyphs. The action motif is permitted by the original icon: it is not an added ambient effect. No opaque backdrop, checkerboard, unbounded bloom, shadow or clipping is introduced.

The green plus, injector and gloved hand read distinctly at actual 48 on white, black and olive. The action arcs remain bounded and do not form a broad haze. A supplemental alpha diagnostic identifies fine arc-edge residue, documented separately; source alpha is not modified.

## Transparency and export evidence

Source alpha spans 0–255. Maximum perimeter alpha is 0; no perimeter samples exceed 1, matching the current exporter allowance for one-step residue. Near-opaque alpha≥240 count is 697821. The actual 48 perimeter is 0. The source's alpha is preserved exactly; most body samples are near-opaque rather than necessarily 255.

Diagnostic runtime derivation fits the entire authored canvas into 44×44, centered on 48×48 with 2px padding, using LANCZOS. It does not crop to visible bounds. White, black and olive composites were inspected at their actual 48 size; the 6× nearest-neighbor image is a separate debug aid and is not native-size evidence. Source-ready status is separate from formal export and live-game proof.

## Supplemental alpha caveat

The 8px-eroded alpha≥8 mask finds 254 below 240 samples (minimum 105), localized in the healing-action arc region, bbox [286,529,660,1063]. The diagnostic is not an acceptance gate and does not prove body translucence. At source zoom there is fine colored antialias residue around a few arc edges. White/black 512 and actual 48 composites show bounded strokes and no broad haze; this is retained and disclosed. alpha-investigation.json and a native crop preserve the evidence. No alpha repair was applied.

## Handoff boundaries

No main/code/Git changes. No source-overrides entry was written here. Parent is expected to copy this family into the C02 pack at packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/skills/c02-skill-field-heal/master.png and separately validate formal export. The proposed fragment is metadata for that integration, not proof it happened. No category-total increment or live-consumer claim is made.
