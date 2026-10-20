# FX20 blocking crescent review

The original atlas 05 row 4 shows a narrow blue-gray blocking stroke, convex to the right and open to the left. Its amber mid-right impact grows into a spray then leaves sparse embers. It is not a solid shield face, and the new drawing preserves the open negative space. The old magenta RGB artwork was identity reference only.

## Phase interpretation

1. bare-crescent: intact narrow defensive arc without gold impact.
2. contact-spark: compact gold starburst at the fixed mid-right attachment, plus six detached sparks.
3. spark-spray: larger starburst at the same attachment, with eleven detached spark components and a long upper streak.
4. residual-sparks: unchanged arc with six sparse right-side embers and no central burst.

## Measurements and caveats

- The slender crescent remains fixed while the impact shape and detached spark cohort change. Fixed upper/lower material ROI best offsets are at most 1 source pixel per axis; blue-color diagnostic bounds vary by up to 9 source pixels, or 0.201 logical pixels. The colored-bound variation partly reflects shading/occlusion, not rigid-body travel.
- At rows 590 through 630, the leftmost gold contact boundary in the initial and peak impact differs by at most 1 source pixel, or 0.022 logical pixels. This verifies attachment at the same mid-right contour, not an identical burst silhouette or centroid.
- The contact phase has a compact starburst and six detached gold components; peak has a larger starburst and eleven detached components; terminal has six small detached gold components and no burst. The upper large streak and left-side shards retire; six terminal right-side embers are not all provable one-to-one continuations of the peak particles.
- Spark paths and sizes are independently authored, not a constant-speed particle simulation. The peak-to-residual change removes the central star and much of the spray in one frame. Source alpha mass changes by +24.0%, +39.5%, then -36.9%, so the 12 fps peak decay may feel abrupt.
- At native 32, fine residual embers and small chips are visually subordinate, especially against black/dark earth and bright stone; they are clearer at 64. The crescent silhouette and main impact remain readable across all six backgrounds.
- The last pose retains the intact crescent with sparse sparks. No blank ending, runtime loop, extra final-frame duration, scene removal rule or 420/500 ms lifetime is inferred for this catalog-only effect. Only encoding and static phase review were verified; browser/game playback remains unobserved.

## Verification

Full-resolution actual black/white composites and green-ground overview were inspected. Native 32/64 cells were inspected on black, white, bright stone, green ground, dark earth and checker. Full 1–2–3–4 and terminal 3–4–1–2 comparisons were inspected. The terminal comparison is not a runtime loop. Parent accepted static semantics.

All four source edges are exactly alpha 0. Every exported cell has a two-logical-pixel alpha0 border. All 28 APNGs and 112 decoded frames were checked: 14 canonical files use four equal 1/12-second frames and one play; 14 slow repeating diagnostics are separate. Actual live playback remains unobserved.
