# C01-FX-02 piercing-hit artwork review

## Result

Four genuine imagegen phases are complete and preserved, with exact request and prompt records. Native 32px/64px static art and alpha review passed for readability, phase meaning, style continuity, and clear outer edges. **Strict lower-contact registration is not yet accepted:** the generated needle tip drifts despite the unchanged shared canvas. This is documented below rather than hidden by per-frame fitting.

No repository or Git files were changed. The canonical FX exporter was not extended or run for FX-02. This folder's deterministic previews reproduce its existing full-canvas resize/paste operation. No browser animation or live gameplay was observed.

## Source and transform

- `onset`, `sweep`, `peak`, and `dissipate` each contain byte-identical `raw.png` and `master.png`, generated separately with the built-in imagegen tool
- Every master is 1254×1254 RGBA; every source and exported preview perimeter has alpha zero
- The exact prompts, requests, reference-file hashes, raw source paths, and SHA-256 records are preserved with each phase
- Uniform source scale: 28/1254; logical origin [2,2]; source pivot [627,627] maps to logical [16,16]
- Native frames: 32×32 and 64×64, both directly derived from the same full authored canvases
- No cropping, independent silhouette fit, per-phase coordinate offsets, source alpha cleanup, or opacity normalization

## Static findings

The onset is a narrow ivory/silver needle with two dark angular chips. Sweep lengthens the needle and adds outward chips and two small side slashes. Peak has a stronger central puncture with jagged side teeth, two large outward chips, and a few limited detached flecks. Dissipate returns to a faint narrow needle and two small residual chips. The vertical puncture axis remains visually stable; no actor, physical spear shaft, target body, damage number, secondary burst, opaque plate, or broad haze was introduced.

The effect is readable at native 32px. The largest phase is intentionally thin and vertical rather than a broad hit flash, consistent with the original source. Light, dark, olive, white, and black backgrounds were inspected at native 32px and 64px. There is no visible magenta-key residue or large white fringe at those display sizes. Fully magnified raw edges contain small low-alpha colored edge noise; no cleanup was performed and no obvious colored fringe survives the native preview.

| Phase | 32px alpha-equivalent area | 64px alpha-equivalent area | 32px max alpha |
|---|---:|---:|---:|
| onset | 34.075 | 130.482 | 255 |
| sweep | 50.984 | 196.875 | 255 |
| peak | 76.816 | 301.427 | 255 |
| dissipate | 9.710 | 35.333 | 109 |

Dissipate has genuine translucent source alpha, maximum 182, with no near-opaque source pixels. It contains about 12.6% of the peak alpha mass at native 32px. Its residual needle remains faintly visible over dark/olive and becomes very subtle over light backgrounds. No opacity boost is recommended.

## Registration caveat

The common canvas, pivot, vertical axis, and full-canvas export transform are unchanged across all phases. However, imagegen did not hold the bottom contour exactly as requested:

| Phase | Central needle bottom y, alpha >16 | Logical y after shared transform |
|---|---:|---:|
| onset | 1150 | 27.678 |
| sweep | 1167 | 28.057 |
| peak | 1205 | 28.906 |
| dissipate | 1132 | 27.276 |

The total 73-source-pixel difference is 1.630 logical pixels, or 3.260 HD pixels. Peak extends about 1.23 logical pixels below onset. The original requirement was a fixed lower impact point, so this set must not be represented as proving exact contact lock. Accept the small artistic contour change explicitly or use a targeted imagegen revision; do not translate or independently fit phases to conceal it.

## Timing and files

The staged APNG and HTML timeline uses 12 fps: onset 0–83⅓ms, sweep 83⅓–166⅔ms, peak 166⅔–250ms, then dissipate held until removal at 420ms. The final frame is held for 170ms; no additional synthetic fade is applied. Review previews repeat only after a 600ms clear interval; gameplay remains non-looping.

All ten APNG files were decoded and checked against their exact native background composites. Their CRCs and rational frame durations passed verification in `apng-verification.json`. This verifies encoding, not observed browser playback.

- `native-32px-five-backgrounds.png` and `native-64px-five-backgrounds.png`: true native-size contact sheets
- `diagnostic-64px-nearest-4x.png`: explicitly magnified inspection only
- `fx-02@1x.png` and `fx-02@2x.png`: deterministic transparent preview strips
- `playback-{light,dark,olive,white,black}@{1,2}x.apng`: exact timing previews
- `playback.html`: standalone preview controls, native sizes, and diagnostic magnification
- `provenance.json`: source/output metrics, full preserved-source hashes, transform, timing, and limitations
- `build_review.py` and `_preview_helpers.py`: reproducible shared-workspace preview builder

The game's existing independent white-hit burst and damage-number overlay are absent from these art-only previews. Their interaction and live render texture resolution remain to be reviewed by the integration owner.
