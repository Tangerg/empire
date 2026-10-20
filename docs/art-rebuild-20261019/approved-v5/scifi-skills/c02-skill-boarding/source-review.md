# 接舷突入 — c02-skill-boarding

## Source audit

The canonical manifest identifies C02-SFV1-skill-boarding as a 48 × 48 skill icon with anchor [24, 24]. The exact original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-boarding.png` (SHA-256 `a8b6d3332b42750f6305746c688f12b8b4b25b2e9be79f80628daf3955dc532e`).

The visually matching original is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-01.png`, SHA-256 `896d2a36f1428d669e2dcdd36c8a3e3cb8801613600e20f6148674993d355fc1`. Its zero-based slot index is 10, row 3, column 3 (one-based). The exact crop is [632, 632, 935, 935] and measures 303 × 303. `source-fullboard.png`, `source-master-slot.png` and `source-runtime.png` preserve the evidence. Catalog order agrees, but was not used instead of inspecting the pixels.

The legacy builder's four pure image functions plus its original chroma postfilters were replayed in memory on the existing original alpha board. The result equals the original runtime in all 2,304 RGBA pixels: 0 differing pixels. No builder main, chroma subprocess or repository write ran. This is source verification only; none of the legacy alpha processing was applied to the new artwork. See `legacy-runtime-reconstruction.json`.

The canonical label and depicted action agree. There is no source/label mismatch, substituted slot or invented gameplay rule. This is a source-faithful stylistic redraw.

## Selected image and semantic review

One dark running humanoid crosses a broken cyan barrier in a thick tan sci-fi doorway, with displaced rubble at the right and lower edge. The dark body separates from the cyan breach plane.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA image with genuine transparency. No rejected attempt exists. The exact prompt, request and reference hashes are retained. The approved crate reference supplies chunky matte sci-fi material language only; its subject was not copied.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the generated output. SHA-256: `862ec81c7edc24fca2798115d82ce05e2d7d5a38a4e5bac3d1cbffd342bc2127`. No crop, paint, alpha cleanup, recolor or post-generation source edit occurred.

## Alpha and small-size QA

All source canvas edges have alpha at most 1; no edge pixel has alpha ≥ 2. The visible alpha ≥ 8 bounding box is [89, 121, 1187, 1152], with left/top/right/bottom margins [89, 121, 67, 102]. Alpha spans 0–255 and 842,598 pixels have alpha ≥ 240. The generated exporter uses many nearly opaque values rather than 255; this is preserved rather than normalized.

The optional 8-pixel eroded-core diagnostic found 0 pixels below alpha 240. This diagnostic is not an acceptance gate. No such internal low-alpha pixel was found.

Full masters, source white/black/olive composites and actual 48 × 48 diagnostics were inspected. The parent independently reviewed the source relation and native-size composites. No broad exterior glow curtain was observed. The diagnostic render preserves the complete source canvas, contained to 44 × 44 and centered in 48 × 48 with 2-pixel padding. It does not crop to the visible bounding box. All actual 48px border alpha values are 0.

Individual rubble chips and fine door seams merge at 48 pixels. The doorway, running body and breach action remain legible. The result contains more rubble pieces than the prompt preference, without altering the source action.

## Scope and integration boundary

This bundle is source-ready for the parent's static-icon binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline QA derivatives, not installed runtime files. Anchor [24, 24] and 48 × 48 dimensions are unchanged. No repository files, manifests, game consumers or Git state were changed by this worker. Runtime integration, live game screenshots and repository regression tests were not performed here and are not claimed. The parent is handling the separate tested binding.

Common reproducible QA scripts are `../build-quartet-qa.py` and `../audit-quartet-sources.py`. The copies in this bundle document those scripts; they assume execution from the common skill-output directory.
