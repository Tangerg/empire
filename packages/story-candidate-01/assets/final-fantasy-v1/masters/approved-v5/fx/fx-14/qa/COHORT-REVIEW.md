# FX14 phase and material review

Status: source alpha, export encoding and static phase review complete. The parent accepted the full/native/terminal static review on 2026-10-20. Browser and live game playback have not been observed.

## Source identity and timing

The exact original atlas 04, row 2 is retained under `reference/`. Its four poses show low green-gray fumes, rising twisting fumes and lifted papers, the largest curled plume, then lower wisps and settling papers. The irregular soil, stones, dry grass and tan name papers are meaningful material. The patch is not a removable matte. The story association is graveyard memory/name-paper pollution, without a new skull, demon or poison-bottle motif.

The current catalog contract is four 32-pixel frames at 12 fps, normal blend, anchor [16,16], loop false. The source audit's historical [16,24] anchor is not the current contract. Four exact 1/12-second frames total 1/3 second. One-play APNGs encode `numPlays=1`; the displayed final image remains after playback, but there is no authored scene consumer to establish a removal time. In particular no 420 ms hit-node lifetime is inferred. Separate 3 fps repeating files are clearly marked diagnostic.

## Stable material, separate motion

`material-registration-proof.json` measures six fixed source-coordinate ROIs: both ground papers, grass and three stones. Relative to rising-fumes, selected low-fumes and crest paper ROIs have best offsets [0,0]. The final phase has offsets [0,1] or [1,1]. Other fixed features differ by at most 1 source pixel. Left/right/bottom soil extrema vary at most 2 source pixels, approximately 0.045 logical pixels after the common scale. These are drawing variations, not exported shifts. Shading is not byte-identical, so subtle texture flicker remains possible.

Initial attempts repeatedly shifted the base down when extending the plume. All are preserved. With parent approval, the tall phase became the common composition master, then the low phase was authored by local upper-region editing. The crest and final phase use that same registered family. No per-frame fitting, rescaling, translation, cropping, compositing repair or alpha cleanup was performed by code.

## Authored motion

1. Low-fumes has two separate low curls. Their alpha128 upper bounds begin at y447 and y563. Both ground papers remain on the soil; no flying paper is present.
2. Rising-fumes extends the curls to y90 and y389. This is a large upward change, especially 357 source pixels for the left tip, or 7.97 logical pixels. Two additional airborne papers appear. They are not the stationary ground papers lifting away.
3. Plume-crest broadens the upper region into connected curled folds. Its top remains around y88, so the step is principally a surge in width and area, not another large upward displacement. The two airborne paper warm-interior centroids advance from approximately (334,507) and (1088,323) to (337,380) and (970,191), while their angles change. Three tiny fragments accompany the crest.
4. Settling-wisps retires the broad crest and leaves two slender curls with upper bounds y374 and y535, plus a separate curved remnant. The airborne paper interiors descend toward (291,637) and (1108,677). The two ground papers remain fixed. The final pose is nonblank and is not intended to join frame 1 seamlessly.

These are pose and cohort observations, not conserved material-particle tracks. There is no unique optical-flow claim for the interior shading, folds or fragments.

## Actual caveats

- Four distinct poses over one third of a second can feel abrupt. The initial rise is large, the next step widens, and the last step collapses; the cadence is uneven.
- Whole-source alpha mass changes by +12.3%, +25.8%, then -26.7%. The final reduction is authored contour loss, not a global opacity fade. It may read as a fast plume collapse.
- Tiny crest fragments and the detached final remnant are subordinate at 32 pixels and clearer at 64 pixels.
- The final airborne papers remain above the patch. A completed physical landing is not shown.
- The 3-4-1-2 image is explicitly a terminal/restart comparison, not a runtime loop seam. Reset continuity is not claimed for this nonloop asset.
- Live playback is unobserved. The known browser/GPU blocker was not re-escalated. Encoding, exact timing, frame identity and static contact have been checked offline.

## Visual and alpha checks

All four full-source black/white pairs, the green-ground source overview, and native 32/64 views on black, white, bright stone, green ground, dark earth and checker backgrounds were inspected. The exterior has no visible backing card, clipped edge, colored matte fringe or rectangular haze. Intentional soil remains dark on dark backgrounds; the sage plumes and tan papers carry the contrast.

Every raw/master pair is byte-identical and genuinely RGBA. All four source perimeters have maximum alpha 1, retained without cleanup; actual composite perimeter channel deltas are recorded in `source-edge-composite-proof.json`. All native exports have the required alpha-zero two-logical-pixel border. All 28 APNGs and all 112 decoded frames match their expected exports, with exact rational delays verified from PNG chunks.
