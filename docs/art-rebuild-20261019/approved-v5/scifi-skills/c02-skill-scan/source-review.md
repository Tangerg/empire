# c02-skill-scan source review

## Identity and evidence

Canonical label: 协议扫描. Category skill, actual runtime 48×48, center anchor [24,24]. No HD entry or animation sequence is introduced.

The actual runtime icon was visually inspected before mapping. The matching original higher-resolution slot is skills-board-01, row 3 column 2 (one-based), exact 5px-inset crop [319, 632, 622, 935]. The full original board, exact crop and original runtime are retained. Topic-catalog order independently agrees; alphabetical final-manifest order was not used. No label/pixel conflict was found. ART-DIRECTION and runtime export rules were read. Input/source hashes are retained.

Original r3c2 dome scanner, squat functional base, five endpoint squares and five segmented fan spokes are retained. The endpoint count is exactly five. Dome is near-opaque painted blue; highlights are bounded. Small base seams and panel accents are illustrative simplification, not new gameplay semantics.

## Generation and native-size review

One built-in image_gen call, transparent_background=true. Output is 1254×1254 RGBA. master.png, raw.png and raw-native-v1.png are byte-identical. No source pixel painting, recoloring, cropping, chroma removal, alpha cleanup or sharpening was performed. There are no rejected generation attempts.

Native source and source 512 composites were inspected. Style uses bold contours, large simplified matte planes and bounded functional action glyphs. The action motif is permitted by the original icon: it is not an added ambient effect. No opaque backdrop, checkerboard, unbounded bloom, shadow or clipping is introduced.

The five-node fan and scanner base remain identifiable at actual 48 on white, black and olive. Some tiny gap distinctions compress, but no endpoint count or overall scanning meaning is lost.

## Transparency and export evidence

Source alpha spans 0–255. Maximum perimeter alpha is 0; no perimeter samples exceed 1, matching the current exporter allowance for one-step residue. Near-opaque alpha≥240 count is 470588. The actual 48 perimeter is 0. The source's alpha is preserved exactly; most body samples are near-opaque rather than necessarily 255.

Diagnostic runtime derivation fits the entire authored canvas into 44×44, centered on 48×48 with 2px padding, using LANCZOS. It does not crop to visible bounds. White, black and olive composites were inspected at their actual 48 size; the 6× nearest-neighbor image is a separate debug aid and is not native-size evidence. Source-ready status is separate from formal export and live-game proof.

## Handoff boundaries

No main/code/Git changes. No source-overrides entry was written here. Parent is expected to copy this family into the C02 pack at packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/skills/c02-skill-scan/master.png and separately validate formal export. The proposed fragment is metadata for that integration, not proof it happened. No category-total increment or live-consumer claim is made.
