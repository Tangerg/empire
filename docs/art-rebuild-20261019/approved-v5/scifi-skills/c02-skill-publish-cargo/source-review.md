# 公开货舱 — c02-skill-publish-cargo

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-publish-cargo` as `公开货舱`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-publish-cargo.png`; SHA-256 `befd9af9b0a056de6557df65240ff307bf3af93f88f0d1caccdfa70b2baa045d`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-03.png`, SHA-256 `2105e869b9263fa4a4ba750ea245e912c60a98fa6d474f4cdf53545e91043e6b`. The global zero-based topic index is 39; within that board the zero-based cell index is 7, row 2, column 4 (one-based). The exact source crop box is [945, 319, 1249, 622], measuring 304 × 303. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The label and depicted action agree. No semantic crop defect or substituted cell was found. Source magenta backing and legacy fringe are retained as evidence, not copied into the new image.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, or repository save ran during this in-memory replay. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

One closed blue-gray/tan cargo container with an orange latch is encircled by exactly two large curved cyan arrows. The closed-case source meaning is retained rather than changed into an open treasure chest.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction. Any repair-node card recovery and contamination removal are documented above; all four depicted actions agree with their labels. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `0bbc6c72595725dbeeee5f7d3d3ccb713d37ce4d0e62919bb33af8ef1eee118e`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 857,342 alpha-zero pixels and 684,062 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 0, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [79, 126, 1195, 1155], with left/top/right/bottom margins [79, 126, 59, 99].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. No eroded-core pixel below alpha 240 was found.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

Fine box seams and arrow-face shading simplify at 48 pixels. The case, latch accent and two circulating arrows remain readable. The narrowest visible margin is 59 pixels, below the requested 8–10 percent, with both arrowheads intact.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-evac-cargo-node-rider.py`; local copies preserve their implementation and expect the common output directory when executed.
