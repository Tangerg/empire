# 沙帆机动 — c02-skill-sand-sail

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-sand-sail` as `沙帆机动`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-sand-sail.png`; SHA-256 `2c9f05b5d165e0fadbeb2d4fe01b6dd76d25f0759329f2442001e379e7ca68b4`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-01.png`, SHA-256 `896d2a36f1428d669e2dcdd36c8a3e3cb8801613600e20f6148674993d355fc1`. The global zero-based topic index is 12; within that board the zero-based cell index is 12, row 4, column 1 (one-based). The exact source crop box is [5, 945, 309, 1249], measuring 304 × 304. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The label and depicted action agree. No semantic crop defect or substituted cell was found. Source magenta backing and legacy fringe are retained as evidence, not copied into the new image.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, or repository save ran during this in-memory replay. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

One short unoccupied dual-runner sail-sled moves above a small bounded sand wake, beside one pale-cyan zigzag movement arrow. The warm ivory triangular sail, short mounting bar and separated runner fronts preserve the source vehicle.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction. The rail-permission source card recovery and background exclusion are documented above; the depicted action agrees with its label. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `8f2aa8f8c428ca9cfc69f1ab4fbf13857a900a129298002dc5977f0c396c5efb`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 1,012,864 alpha-zero pixels and 530,651 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 1, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [72, 114, 1190, 1161], with left/top/right/bottom margins [72, 114, 64, 93].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. Twelve pixels in the 4 × 5-pixel region x=615–618, y=838–842 have alpha below 240, with minimum 44. The white/black diagnostic crop locates this tiny region at the runner/sand-wake boundary. No broad object core is translucent.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

Fine runner divisions and sail shading simplify at 48 pixels. The sail, two runner fronts and maneuver arrow remain readable. The sand wake is a bounded local movement shape; it is not a terrain tile or soft exterior dust cloud. Some visible margins are narrower than the requested 8–10 percent but all arrow and sail tips remain intact.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-ice-sand-vacuum-rail.py`; local copies preserve their implementation and expect the common output directory when executed.
