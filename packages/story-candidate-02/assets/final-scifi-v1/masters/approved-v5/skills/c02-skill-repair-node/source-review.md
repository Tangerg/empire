# 修复节点 — c02-skill-repair-node

## Exact source and runtime audit

The canonical manifest identifies `C02-SFV1-skill-repair-node` as `修复节点`, a 48 × 48 skill icon with anchor [24, 24]. The actual original runtime is `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-repair-node.png`; SHA-256 `911da5b48cd24bbdabc867ee7eb9228e536ae4692d5ba148cd52f3a2fdf7092d`.

The matching original board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-02.png`, SHA-256 `1e7fcd63eff342b1f3fc129a9913d78944603a4c19eeaed68e0b9128e2b4d50c`. The global zero-based topic index is 24; within that board the zero-based cell index is 8, row 3, column 1 (one-based). The exact source crop box is [5, 632, 309, 935], measuring 304 × 303. Source board, exact cell and original runtime are retained separately.

The original 48px pixels were inspected before matching the full board. Catalog order agrees independently; alphabetic manifest order was not used to guess cells. The uniform legacy slot [5, 632, 309, 935] includes the top strip of the next rail-switch card at y=913–934 and clips the intended card right strip beyond x=309. The original runtime therefore carries scene backing and a detached unrelated rail fragment. The complete intended node-repair card was recovered separately at [34, 638, 315, 892] (281 × 254), saved as `source-complete-motif.png` and used as the generation reference. The new drawing restores the complete node platform and maintenance arm housing while excluding unrelated rails and environmental wall/floor backing. The node-service action and functional platform remain; no new gameplay mechanism was invented. Exact legacy slot and runtime are preserved separately.

For stronger verification, four pure functions from the original builder and its existing chroma postfilters were replayed in memory on the existing original alpha board. The recreated 48 × 48 image equals the original runtime in every RGBA pixel: 0 differing pixels. No builder main, chroma helper, or repository save ran during this in-memory replay. See `legacy-runtime-reconstruction.json`. These old cleanup rules were never applied to the generated artwork.

## Selected artwork

One short technical node with a seated cyan top cell stands on a squat octagonal service platform. One articulated mechanical claw works at its side with a small localized cyan service mark. The arm housing and functional platform are complete; the unrelated rail-switch strip and scene backing are absent.

The built-in image generation tool produced one selected native 1254 × 1254 RGBA cutout. This is source-faithful stylistic reconstruction. Any repair-node card recovery and contamination removal are documented above; all four depicted actions agree with their labels. Approved cargo-crate artwork supplies material and contour style only. Exact prompt, request, source/reference images and hashes are retained. There was one generation and no rejected attempt.

`raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical to the original generated file: SHA-256 `a727ea3cdadad0c8c816192d1a68da4bcf008afd7dc4c39d68f982e10b89c8a6`. No source painting, crop, alpha normalization, chroma keying, cleanup or recolor was performed.

## Alpha and native-size review

The source is genuine RGBA with alpha extrema 0–255. There are 792,326 alpha-zero pixels and 762,916 pixels with alpha ≥ 240. The original generated exporter uses many near-opaque values rather than alpha 255; these values were not normalized. Source canvas edge alpha is at most 1, with 0 border pixels at alpha ≥ 2. The visible alpha ≥ 8 box is [80, 83, 1201, 1186], with left/top/right/bottom margins [80, 83, 53, 68].

The optional 8-pixel eroded-core diagnostic is reported rather than treated as an acceptance gate. No eroded-core pixel below alpha 240 was found.

Full selected masters, 256px white/black/olive source composites and the actual 48 × 48 diagnostics were visually inspected. There is no broad exterior glow curtain. The diagnostic uses complete-source Lanczos contain 44 × 44 centered in a 48 × 48 transparent canvas with 2-pixel padding; it does not crop the visible bounding box. All actual 48px border alpha values are 0. The parent independently approved the static review.

Small pincer hinges, energy-cell facets and service-mark points simplify at 48 pixels. The node-plus-maintenance-arm silhouette remains legible. The local service mark is retained as bounded action color; no broad exterior aura appears. Some margins are below the requested 8–10 percent, with no clipped equipment.

## Delivery and integration boundary

The bundle is source-ready for parent review/binding. `diagnostic-native48/runtime48-diagnostic.png` and all composites are offline diagnostics, not installed game runtime. The 48 × 48 dimensions and [24, 24] anchor are preserved. No repository or Git writes were made. This worker did not run game regression tests, install consumer bindings or verify live game/browser rendering. These separate steps are not claimed.

The executed common scripts are `../build-quartet-qa.py` and `../audit-evac-cargo-node-rider.py`; local copies preserve their implementation and expect the common output directory when executed.
