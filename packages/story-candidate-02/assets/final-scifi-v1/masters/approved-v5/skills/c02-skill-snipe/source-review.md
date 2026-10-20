# 长束狙击 — c02-skill-snipe

## Source audit

The canonical manifest identifies C02-SFV1-skill-snipe as a 48 × 48 skill icon with anchor [24, 24]. The exact original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-snipe.png` (SHA-256 `df514114335beba418a3fb334be4f00eb7f7208ff7e204887a0998e096a9c30f`).

The visually matching original is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-01.png`, SHA-256 `896d2a36f1428d669e2dcdd36c8a3e3cb8801613600e20f6148674993d355fc1`. Its zero-based slot index is 4, row 2, column 1 (one-based). The exact crop is [5, 319, 309, 622] and measures 304 × 303. `source-fullboard.png`, `source-master-slot.png` and `source-runtime.png` preserve the evidence. Catalog order agrees, but was not used instead of inspecting the pixels.

The legacy builder's four pure image functions plus its original chroma postfilters were replayed in memory on the existing original alpha board. The result equals the original runtime in all 2,304 RGBA pixels: 0 differing pixels. No builder main, chroma subprocess or repository write ran. This is source verification only; none of the legacy alpha processing was applied to the new artwork. See `legacy-runtime-reconstruction.json`.

The canonical label and depicted action agree. There is no source/label mismatch, substituted slot or invented gameplay rule. This is a source-faithful stylistic redraw.

## Selected image and semantic review

The squat lower-left gun, narrow diagonal orange beam, upper-right circular reticle and central hit star remain separately readable. The ring and all four aiming ticks are complete.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA image with genuine transparency. No rejected attempt exists. The exact prompt, request and reference hashes are retained. The approved crate reference supplies chunky matte sci-fi material language only; its subject was not copied.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the generated output. SHA-256: `46bde23bc51229b72ac2e7a98c96750801d09f6290741270ba0bebd9cf7c1062`. No crop, paint, alpha cleanup, recolor or post-generation source edit occurred.

## Alpha and small-size QA

All source canvas edges have alpha at most 1; no edge pixel has alpha ≥ 2. The visible alpha ≥ 8 bounding box is [65, 104, 1220, 1181], with left/top/right/bottom margins [65, 104, 34, 73]. Alpha spans 0–255 and 285,156 pixels have alpha ≥ 240. The generated exporter uses many nearly opaque values rather than 255; this is preserved rather than normalized.

The optional 8-pixel eroded-core diagnostic found 0 pixels below alpha 240. This diagnostic is not an acceptance gate. No such internal low-alpha pixel was found.

Full masters, source white/black/olive composites and actual 48 × 48 diagnostics were inspected. The parent independently reviewed the source relation and native-size composites. No broad exterior glow curtain was observed. The diagnostic render preserves the complete source canvas, contained to 44 × 44 and centered in 48 × 48 with 2-pixel padding. It does not crop to the visible bounding box. All actual 48px border alpha values are 0.

The small star points, gun panel seams and muzzle rings simplify at 48 pixels. The required focus-and-target relation remains clear. The right visible margin is 34 native pixels, below the requested 7 percent but complete and unclipped.

## Scope and integration boundary

This bundle is source-ready for the parent's static-icon binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline QA derivatives, not installed runtime files. Anchor [24, 24] and 48 × 48 dimensions are unchanged. No repository files, manifests, game consumers or Git state were changed by this worker. Runtime integration, live game screenshots and repository regression tests were not performed here and are not claimed. The parent is handling the separate tested binding.

Common reproducible QA scripts are `../build-quartet-qa.py` and `../audit-quartet-sources.py`. The copies in this bundle document those scripts; they assume execution from the common skill-output directory.
