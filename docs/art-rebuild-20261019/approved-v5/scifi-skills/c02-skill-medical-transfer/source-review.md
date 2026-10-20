# 医疗转运 — c02-skill-medical-transfer

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-medical-transfer` as `医疗转运`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-medical-transfer.png`; SHA-256 `3219df4c58b8f30b5335c559601d9d70f826f89e2f06235d32bbeb078c2ccfb5`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-03.png`, SHA-256 `2105e869b9263fa4a4ba750ea245e912c60a98fa6d474f4cdf53545e91043e6b`. The global zero-based topic index is 38; within that board the zero-based cell index is 6, row 2, column 3 (one-based). The exact source crop box is [632, 319, 935, 622], measuring 303 × 303. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The label and depicted action agree. No source mismatch, corrupted semantic crop, substituted cell or unsupported gameplay interpretation was found. The original magenta backing and faint legacy color fringe are evidence, not required artwork.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, repository save or external command ran. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

One calm, intact, anonymous patient lies on a wheeled diagonal stretcher beneath one compact care drone with two short articulated side arms. A cyan chest plus and short, bounded care plane behind the upper torso retain the medical-transfer cue.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction, not a label-driven replacement of a conflicting source. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `3c0e13c115a0d7f1095760b61dda48c1ceb7059aacb5bc406ff608f454ce4f3e`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 909,187 alpha-zero pixels and 636,672 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 1, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [100, 108, 1154, 1198], with left/top/right/bottom margins [100, 108, 100, 56].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. No eroded-core pixel below alpha 240 was found.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

The small chest plus, caster interiors, finger shapes and drone joint divisions simplify at 48 pixels. The reclined patient, stretcher and overhead care assembly remain legible. The opaque bounded care plane has some tonal interpolation, but is limited to the source-defined functional region and does not create an exterior light curtain. Bottom visible margin is 56 native pixels, below the requested 8 percent but complete.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-rescue-ammo-shield-medical.py`; local copies preserve their implementation and expect the common output directory when executed.
