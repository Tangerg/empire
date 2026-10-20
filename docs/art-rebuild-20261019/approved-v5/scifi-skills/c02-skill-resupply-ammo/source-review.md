# 补给弹药 — c02-skill-resupply-ammo

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-resupply-ammo` as `补给弹药`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-resupply-ammo.png`; SHA-256 `6f488e06de3565cc109bbb8cdef807b65112f53d93b9bbea8be44c23a11a97b6`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-03.png`, SHA-256 `2105e869b9263fa4a4ba750ea245e912c60a98fa6d474f4cdf53545e91043e6b`. The global zero-based topic index is 32; within that board the zero-based cell index is 0, row 1, column 1 (one-based). The exact source crop box is [5, 5, 309, 309], measuring 304 × 304. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The label and depicted action agree. No source mismatch, corrupted semantic crop, substituted cell or unsupported gameplay interpretation was found. The original magenta backing and faint legacy color fringe are evidence, not required artwork.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, repository save or external command ran. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

One three-quarter tan/graphite supply case exposes exactly three large vertical cyan ammunition cartridges in its front rack. The right status panel, broad top plates and short cyan top inset preserve the source arrangement.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction, not a label-driven replacement of a conflicting source. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `8015f5d02ec666f4c6f9682a7d00496b3738bc945bf6e7b44f16f947c0689e2f`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 736,783 alpha-zero pixels and 824,037 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 0, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [104, 140, 1150, 1147], with left/top/right/bottom margins [104, 140, 104, 107].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. No eroded-core pixel below alpha 240 was found.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

Fine cap openings, panel grooves and small status bars simplify at 48 pixels. The three bright vertical cartridges and surrounding compact case remain distinct. The generated status display uses four broad bars rather than the prompt preference of three; this is a secondary nonsemantic detail.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-rescue-ammo-shield-medical.py`; local copies preserve their implementation and expect the common output directory when executed.
