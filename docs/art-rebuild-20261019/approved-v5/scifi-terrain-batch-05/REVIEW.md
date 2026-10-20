# C02 Nereia terrain: four native-reviewed candidates

Generated 2026-10-20 using four independent built-in image_gen.imagegen requests. Sources and preview diagnostics only: this worker changed no shared exporter, catalog, runtime, manifest or Git state. No CLI fallback or HD assets.

## Original identity and ordering

The next canonical manifest-order family after Milight is Nereia: deep-water, floating-platform, reverse-current, shallow-tide. The full original terrain-board-02.png was inspected, not inferred from names. The original builder consumes each atlas row-major in four columns; docs/story-candidates/candidate-02/art-assets/07-topic-catalog.md lines 197–200 confirms original topic order floating-platform, shallow-tide, deep-water, reverse-current, hence board 02 top-row columns 1–4. The builder's original manifest-complete.json is not present in this checkout; mapping is independently corroborated by that topic catalog, full board and original native runtime strips. Original atlas and strips are copied unchanged in original-reference, with strip hashes in source-inventory.json and reference hashes in each provenance.json.

## Sources and registration

Four opaque RGB 1254×1254 native generator outputs are preserved byte-identically as raw.png and master.png. Each source directory contains the exact prompt.txt, request.json, generated path in generation-result.json, and hashed provenance.json. There was no source editing, cleanup, scaling, aspect fitting, hidden crop or color repair. Original atlas is subject reference only; approved Farlight cargo master is style reference only.

Floating-platform uses four explicit equal 627×627 quadrant windows, ordered top-left, top-right, bottom-left, bottom-right. Other topics use the entire 1254-square source. proposed-registration.json contains exact source hashes and windows. The generator's full source files are the deliverables, not review-board crops.

## Native verification and visual judgment

build_review.py uses existing read_material/render_material helpers without a whitelist or other shared-code change. Existing Lanczos native32 sampling, historical masks, and one-pixel opposite-edge matching remain unchanged. All four source masters, original native strips, all-zoom2 and all four connected-grid-native32 images were visually inspected. Connected-grid images use actual adjacency-derived NESW indices on black, white and olive backgrounds; zoom files are nearest-neighbor diagnostics only.

- Deep water retains dark petrol/teal water with subdued submerged stone and broad water-plane contours. It is clearly darker and calmer than the other water types. Low contrast on black is deliberate; the narrow historical connector is not an accessibility overlay. Full source has more small plane subdivisions than requested, but these reduce to coherent broad dark/teal areas at native32 rather than noisy speckles.
- Floating platform retains warm ivory paving, charcoal/brass engineering seams and cyan utility fittings. All four 32px variants are visibly distinct and preserve a simple deck reading. Original perspective pillars, open-water surround and bridge silhouette were intentionally translated into a fully opaque ground surface, not claimed as preserved geometry. No new gameplay obstacles are inferred. Repeated caps and seams remain obvious in a large grid.
- Reverse current retains pale cyan curling foam over dark turquoise water. A broad central reverse bend reads strongly at native32 and distinguishes this from calm deep water. The master has several smaller flat foam fragments despite the simple-ribbon request; native sampling reads as bright turbulent water. This is static material, not animated flow or an actual directional simulation, and repeated current motifs are visible in connected grids.
- Shallow tide retains light aquamarine water, pale sandy bands and low-contrast submerged stone forms, visibly brighter than deep water. Smaller stone marks are present in the complete master; native32 suppresses these into the shallow-water texture. There is no exposed rocky shore or coral obstacle. Lightest pixels approach white but turquoise outlines remain visible on white.

All historical alpha bytes match original runtime strips exactly. Distinct cell counts are 16/4/16/16. Exact allowed-pair edge checks pass: 64 east-west plus 64 south-north per connected topic, 16 plus 16 for the four platform variants. Those checks establish exact outermost-edge equality, not seamless interior wave continuation or absence of repetitive motifs. The inherited narrow cross/capsule alpha geometry remains conspicuous in connected-grid diagnostics; it was not redesigned or concealed.

Recommendation: accept all four as native-reviewed source candidates for parent registration/export. Formal shared registration, runtime export, whole-pack validation and consumer/runtime inspection remain the parent's responsibility and are not claimed complete here.
