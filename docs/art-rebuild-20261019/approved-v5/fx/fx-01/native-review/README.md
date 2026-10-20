# C01-FX-01 four-phase artwork QA

## Result

**Accepted for staged static/semantic art review. Live gameplay and browser animation observation remain unverified.**

The four unchanged RGBA masters depict an open-left ivory/silver crescent, a broader sweep, the largest toothed peak, and separated translucent remnants. Native 32px and 64px composites preserve that progression. The native review was inspected on light stone, dark earth, olive, white, and black backgrounds. No unrelated opaque plate, magenta key residue, or visible white background fringe was found. Intentional charcoal contour planes remain part of the crescent.

The phases are meaningfully distinct: both arc extent and thickness grow; trailing cuts change rather than the sequence repeating one pose; the last phase breaks the body into sparse remnants. Shared geometry stays fixed. A changing alpha-weighted centroid is expected as an open crescent grows and is not a per-phase positioning adjustment.

## Proven export contract

- All four raw and master pairs are byte-identical PNG RGBA, exactly 1254×1254
- All original raw images, masters, prompts, requests, and reference hashes were preserved; before/after SHA-256 values are in `provenance.json`
- The exact exporter code from commit `49b4afc6c5b4cde520ee8258a74c8140a5780657` was used, verified against the checkout's equivalent code
- Export/check both passed in a standalone fixture beneath this QA folder. No source registration or repository output was written by this review
- Whole authored canvas scale is `28/1254`, origin `[2,2]`, source pivot `[627,627]`, logical pivot `[16,16]`
- Both densities derive directly from the same full canvases: 128×32 base strip and 256×64 HD strip
- No cropping, independent bounding-box fit, per-phase offset, alpha cleanup, or opacity normalization
- Source and output perimeters are fully transparent; all four source and all four per-density decoded images differ

## Native-size findings

| Phase | 32px alpha-equivalent area | 64px alpha-equivalent area | Visual assessment |
|---|---:|---:|---|
| onset | 48.765 px | 193.525 px | Compact open crescent, readable light edge |
| sweep | 114.933 px | 457.843 px | Broader, longer crescent with changed lower trailing cut |
| peak | 237.027 px | 947.447 px | Largest extent and strongest pointed trailing topology |
| dissipate | 6.914 px | 25.859 px | Separated faint remnants; continuous body is gone |

Alpha-equivalent area is the sum of alpha/255, used as a diagnostic of growth/decay, not as proof of meaningful animation. At 32px the final phase has about 2.9% of the peak's alpha mass, so its long terminal hold is visually unobtrusive. The final fragments are intentionally faint, especially over light backgrounds. No opacity boost is recommended.

Dissipate source maximum alpha is 187; it has no near-opaque pixels. The exported maximum is 107 at 1× and 195 at 2×. The latter is a normal Lanczos resampling overshoot from the unchanged exporter, not opacity normalization. The source contains scattered alpha=1 residue outside its principal silhouette; it was preserved. It did not produce a visible backing in the native composites.

## Timing and playback evidence

The exact-duration APNGs were decoded and their stored delays verified:

- onset: 0–83⅓ ms (`1/12` second)
- sweep: 83⅓–166⅔ ms (`1/12` second)
- peak: 166⅔–250 ms (`1/12` second)
- dissipate: 250–420 ms (`17/100` second), a 170ms total visible hold
- after 420ms: effect removed; preview has a separate 600ms clear interval before replay

This preserves the fourth frame through the nominal clip end at 333⅓ms until hit removal at 420ms. The game effect is non-looping; only the review preview repeats. No arbitrary fade is added over the held final phase. `playback.html` implements the same timeline and provides play, repeat, pause, scrub, native-size views, the HD strip at 32 CSS pixels, and explicitly labeled diagnostic magnification.

**Browser limitation:** installed Chromium failed before loading the page with `socket() failed: Operation not permitted`. A scoped execution retry failed in the environment wrapper before browser startup. Therefore actual browser playback, runtime renderer texture resolution, and live first-level hit observation were not completed. The APNG frame encoding and static output pixels were verified; do not describe these as observed live gameplay or a completed live animation test.

The existing game's white burst circle and damage number are deliberately absent from these art previews. A live hit-path check with those overlays remains the outstanding integration review.

## Files to inspect

- `native-32px-five-backgrounds.png`: true 32px frames
- `native-64px-five-backgrounds.png`: true 64px frames
- `diagnostic-64px-nearest-4x.png`: explicitly magnified diagnostic sheet
- `playback.html`: standalone staged preview with exact timeline
- `playback-{light,dark,olive,white,black}@{1,2}x.apng`: exact-rational encoded previews
- `fx-01@1x.png`, `fx-01@2x.png`: deterministic transparent strips
- `export-report.json`, `export-check.json`: matching unmodified exporter reports
- `provenance.json`: complete raw/output hashes, transform, measurements, and limitations
- `build_review.py`: reproducible review builder; writes only beneath this QA folder
- `verify_playback.cjs`: staged-browser test prepared but blocked before page launch

No new image generation, raw-source edits, Git edits, commits, pushes, or live-game claims were made by this review.
