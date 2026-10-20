# FX16 phase, tower and tone review

Status: source alpha, native export and static phase review complete. The parent accepted the corrected quartet on 2026-10-20. Browser and live game playback remain unobserved.

## Original identity and deliberate clipping correction

The exact original atlas 04, row 4 and all four inset crop hashes are preserved in `reference/` and `source-evidence.json`. The subject is one squat dark stone tower with an amber top source, straight silver-gray fan rays and a few levitating stones. “Seven Towers” describes the story network, not seven buildings within this effect. The tower remains a physical emitter; it does not collapse or travel with the rays.

The original third frame's central ray reaches the upper crop boundary. The new crest is deliberately complete, with its visible alpha128 tip at source y65 and actual perimeter alpha 0. This repairs clipping instead of copying the defect. The crest was authored first to reserve the full fan's headroom and establish a stable tower canvas; the shorter phases were local imagegen edits.

## Timing

Retain the current manifest: four 32-pixel frames, 12 fps, normal blend, anchor [16,16], loop false. The earlier audit's [16,24] anchor is historical. The canonical APNGs encode exactly one play, four equal 1/12-second frames and a total 1/3-second sequence. APNG retains its final displayed image, but no authored consumer establishes scene removal or a 420 ms lifetime. Separate 3 fps repeating files are diagnostic only. The 3-4-1-2 strip is a terminal/restart comparison, not a loop seam.

## Registration and material correction

The six fixed source-coordinate ROIs cover the amber socket, parapet, doorway, front base and both rubble sides. Their best offsets relative to crest are no more than 1 source pixel on either axis. The lower connected tower bounds vary [1,0,1,2] pixels across the quartet, at most 0.045 logical pixels after the shared scale. These diagnostics do not alter placement.

The first short-rays candidate had a visibly lighter and warmer complete tower. The parent requested one bounded imagegen-only correction, retaining the short ray silhouette and common canvas. The selected replacement is `732e5937...`; its upper ray bounds are unchanged and alpha128 upper-ray intersection-over-union with the earlier candidate is 0.9574. Source pixels are not claimed identical.

Using the same opaque lower-tower mask, mean luminance changed from 81.55 in the rejected short candidate to 65.19 in the selected short phase. Rising, crest and residual phases measure 63.67, 60.90 and 62.55. The large first-frame tone jump is substantially reduced, but small lighting, shading and amber-core differences remain. The parent accepted this residual variation after native review. See `tone-repair-proof.json`; no code-based color adjustment was used.

## Phase and cohort interpretation

1. Short-rays: three compact, straight gray rays, central top y428. No airborne stones. The tower and rubble remain.
2. Rising-fan: seven narrower mid-length rays, central top y285. Six stones appear above and beside the tower. The outer low rays are short, not a uniformly scaled crest.
3. Graylight-crest: seven broad rays fill the largest fan, central top y65. The two high stone centers move from approximately (511,659) and (754,662) to (458,524) and (802,534). Four lower stones also move outward/upward. This supports a levitating cohort, not whole-tower motion.
4. Residual-rays: three thin unequal rays remain; the central ray leans left and reaches y429. The two high stones retire. Four lower stones settle, with the smallest left one reaching/contacting the rubble contour. Only three final stones are separate alpha128 components; claiming all four remain detached would be incorrect.

Ray length changes describe an energy envelope growing and receding, not rigid silver objects being pulled backward. The images do not establish a unique point-by-point physical trajectory for every ray face or stone contour. The final frame is nonblank and does not show complete disappearance or every stone fully landed.

## Cadence and visibility caveats

- Whole-source alpha mass changes by +27.0%, +42.9%, then -47.0%. The last transition is an authored fan collapse, not a global fade; it may read abruptly at 12 fps.
- The central tip advances by 143 then 220 source pixels, followed by shorter residual geometry. Timing is equal but spatial cadence is not uniform.
- Fine stones and dark tower details are subordinate at 32 pixels, especially over black/dark earth. The silver fan and amber cap carry the effect; 64 pixels resolves the masonry better.
- Minor tower shading and emitter brightness changes remain. Neither a perfectly static pixel patch nor smooth live playback is claimed.
- There is no seamless 4-to-1 wrap requirement for this nonloop asset.

## Source and export checks

Full-resolution black/white composites for all selected sources, native 32/64 views on six backgrounds, and full/terminal comparison sheets were inspected. All selected source perimeters have alpha 0. No clipped ray, colored matte fringe, rectangle or broad glow wash appears in the actual composites. Every export has alpha-zero padding of 2 logical pixels, 4 physical pixels at 2x. All 28 APNGs and 112 decoded frames match the expected exports, with exact acTL/fcTL timing verified.

The first crest alternate is retained with an important correction to its initial rejection note: raw-alpha display suggested speckle, but actual black/white composites disproved a matte-fringe failure. It remains an unselected alternate with a small amber halo, not a confirmed alpha failure. The selected local edit has stronger dark ray contours and more confined light.

Known browser/GPU restrictions were not re-escalated. All playback statements are limited to encoded timing and offline static review.
