# 列车权限 — c02-skill-rail-permission

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-rail-permission` as `列车权限`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-rail-permission.png`; SHA-256 `245ec6707b84ea890872daa8b470e1877398edbd51f9cd09931854e5dfc568bd`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-02.png`, SHA-256 `1e7fcd63eff342b1f3fc129a9913d78944603a4c19eeaed68e0b9128e2b4d50c`. The global zero-based topic index is 20; within that board the zero-based cell index is 4, row 2, column 1 (one-based). The exact source crop box is [5, 319, 309, 622], measuring 304 × 303. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The uniform legacy slot ends at x=309, while the complete original card extends to x=315. Its rightmost 6-pixel strip was clipped, and the legacy runtime carries the dark wall/card background. The full intended card was recovered separately at [34, 339, 315, 617] (281 × 278) and saved as `source-complete-motif.png`. That full source was used as image reference. The new drawing restores the complete functional circle and a completed cuff while excluding the source wall/card; no missing gameplay meaning or new symbol was invented. The exact legacy slot remains preserved for comparison.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, or repository save ran during this in-memory replay. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

One gloved index finger presses a cyan permission interface at the center of one circular bronze/graphite access mechanism with amber ring segments. The whole mechanical circle and completed short wrist cuff are visible. The dark source wall/card is excluded.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction. The rail-permission source card recovery and background exclusion are documented above; the depicted action agrees with its label. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `d74ef35506af1b357f1d5171def46ee4c8dbbbbf66077f007230a7538c6d5ae9`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 666,414 alpha-zero pixels and 893,430 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 1, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [63, 55, 1239, 1201], with left/top/right/bottom margins [63, 55, 15, 53].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. No eroded-core pixel below alpha 240 was found.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

Finger joints, small panel seams and bevels simplify at 48 pixels, but the hand pressing the interface remains visible. The complete wrist cuff has a narrow 15-pixel right margin, below the requested 8–10 percent; no finger, cuff or circle edge is clipped. No train, added permission symbol or faction emblem was invented.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-ice-sand-vacuum-rail.py`; local copies preserve their implementation and expect the common output directory when executed.
