# 冰钩拉拽 — c02-skill-ice-hook

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-ice-hook` as `冰钩拉拽`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-ice-hook.png`; SHA-256 `35a8bb496c9406a57a31586afbde863a20ca264fa3a14669ffcadf429d981082`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-01.png`, SHA-256 `896d2a36f1428d669e2dcdd36c8a3e3cb8801613600e20f6148674993d355fc1`. The global zero-based topic index is 15; within that board the zero-based cell index is 15, row 4, column 4 (one-based). The exact source crop box is [945, 945, 1249, 1249], measuring 304 × 304. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The label and depicted action agree. No semantic crop defect or substituted cell was found. Source magenta backing and legacy fringe are retained as evidence, not copied into the new image.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, or repository save ran during this in-memory replay. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

A compact bronze/graphite winch at lower-left joins a large upper-right cyan ice grapple through a short visible chain. Broad icy prongs and three separated ice fragments preserve the pull-and-hook motif. All tips remain complete.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction. The rail-permission source card recovery and background exclusion are documented above; the depicted action agrees with its label. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `2bff8a3dc4cdc60715cadeba446066e01ea10688cbaa9070bb90414db9641626`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 1,161,334 alpha-zero pixels and 387,611 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 1, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [67, 109, 1215, 1176], with left/top/right/bottom margins [67, 109, 39, 78].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. No eroded-core pixel below alpha 240 was found.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

Chain interiors, small winch facets and fine ice subdivisions simplify at 48 pixels. The diagonal joined mechanism and icy hooked head remain recognizable. The smallest visible margin is 39 native pixels, below the requested 8–10 percent but no tip is clipped.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-ice-sand-vacuum-rail.py`; local copies preserve their implementation and expect the common output directory when executed.
