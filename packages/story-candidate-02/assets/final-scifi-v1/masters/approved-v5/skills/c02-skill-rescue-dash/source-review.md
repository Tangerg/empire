# 救援冲刺 — c02-skill-rescue-dash

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-rescue-dash` as `救援冲刺`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-rescue-dash.png`; SHA-256 `64581746eb2e6a08e928b390454eea4c454663c65f3e7129e2f4b13c6453298e`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-01.png`, SHA-256 `896d2a36f1428d669e2dcdd36c8a3e3cb8801613600e20f6148674993d355fc1`. The global zero-based topic index is 2; within that board the zero-based cell index is 2, row 1, column 3 (one-based). The exact source crop box is [632, 5, 935, 309], measuring 303 × 304. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The label and depicted action agree. No source mismatch, corrupted semantic crop, substituted cell or unsupported gameplay interpretation was found. The original magenta backing and faint legacy color fringe are evidence, not required artwork.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, repository save or external command ran. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

Exactly two separate unarmed space-suited figures move toward the upper-right, joined at one central handclasp. The front figure leads the lower-left figure; dark helmets and armor are separated by broad gray planes. Local cyan speed ribbons preserve the rescue dash cue.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction, not a label-driven replacement of a conflicting source. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `38a0f0af1389705e45f6b9af7f9ec556044cca1bd6fdcd149e2abe0746bcbac8`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 1,116,396 alpha-zero pixels and 410,523 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 1, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [26, 252, 1232, 1058], with left/top/right/bottom margins [26, 252, 22, 196].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. Exactly one eroded-core diagnostic pixel at [427, 989] has alpha 239. This localized value is preserved unchanged and does not form an exterior glow or broad translucent core.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

The hand connection remains visible, but individual fingers, armor divisions and visor highlights simplify at 48 pixels. Dark outlines partly merge into black backgrounds while cyan rims and gray limbs preserve the two silhouettes. There are more speed-ribbon pieces than the three requested in the prompt, still bounded and local. Visible left/right margins of 26/22 native pixels are below the requested 8 percent but no component is clipped.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-rescue-ammo-shield-medical.py`; local copies preserve their implementation and expect the common output directory when executed.
