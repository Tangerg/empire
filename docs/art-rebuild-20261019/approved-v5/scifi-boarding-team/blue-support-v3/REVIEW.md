# Blue support v3: accepted bounded source correction, not integrated

One built-in image edit was executed. The raw generated PNG was copied byte-for-byte to `packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/boarding-team/candidates/blue-support-v3/walk-b.png`. Generation provenance beside it contains the exact prompt, raw source path and SHA-256. No second attempt, pixel modification, alpha cleanup, cropping, filtering, source rescaling, or canvas extension occurred. Original quartet and rejected v2 are untouched and remain unbound. No shared registry, runtime, application code, or Git operations were performed.

## Source and anatomy

Before generation, inspected the entire v2 canvas and its bounds: nonzero alpha [0,17,1237,1254], with right hand close to the edge. Whole-group translation was therefore explicitly forbidden in the edit request. V3 is a genuine lower-body redraw, not a translated group. It removes the former blue boot at x675 and replaces that anatomical leg with a planted leg emerging beneath the shield at x1003. The blue other/rightmost boot remains folded back, its sole y1065 versus the planted y1201 (136 source pixels of elevation). Exactly two actors, four anatomical legs and four boots remain. Some proximal blue leg is naturally shield-occluded. Orange rear boot stays lifted while its forward boot plants; blue rightmost boot lifts while its other boot plants. This complements walk-a, in which the blue rightmost boot plants and orange forward boot lifts. Orange cutter/reel and cyan-inset oval shield are intact.

The full source is 1254×1254 RGBA, alpha range0–255. Visible alpha>=128 bounds [39,357,1235,1202] preserve hand and soles inside the canvas. Nonzero-alpha bounds reach left/bottom due to alpha1 traces, not visible clipping; source perimeter maximum1 meets the existing contract. Native perimeter maximum0 for every frame. This is not a claim of mathematically zero alpha on every source-edge pixel.

## Support measurements

`source-foot-rois.png` visibly annotates disjoint foot ROIs; `four-foot-evidence.json` includes lifted feet. Planted contact uses alpha>=128, mean X of the bottom four occupied rows, then equal weights for the two feet. Full orange ROI [420,1090,660,1200] yields x531.6078/y1175. Blue ROI [895,1090,1125,1220] yields x1003.0823/y1201. Their mean x767.3450 meets requested730–770; blue contact meets930–1010 and deepest sole meets1180–1210. The orange ROI was expanded to include its full boot; this did not change the measured bottom center. Lifted boots are excluded from support.

Common renderWidth44 and origin[5,3] are unchanged. Offsets are idle[2,-1], walk-a[2,0], attack[2,0], v3 walk-b[0,0], all integer and within±2. Walk-b projected support is [31.9244,45.1404]; exact X alignment would require only+0.0756, not the forbidden v2+5.85. Quartet support drift is1.1847px X and0.9825px Y. V3 therefore restores registration to roughly the original quartet's X drift, rather than claiming perfect registration. Body contours shift modestly between poses and the two-pixel registration difference remains visible; no per-frame scale hides this.

## Native and shared-renderer review

Unmodified export_frame_overrides.plan passes one existing256×48 output with four64×48 frames. This is a diagnostic only, not a runtime write. Three backgrounds and ordered motion frames preserve two-person identity, cutter and cyan shield. Orange stepping is strong; the blue lifted rear boot is small at native size but its rise and altered planted boot remain distinguishable in the side-by-side review. Full-source review establishes the anatomical reversal rather than inferring it from the structural pass.

`renderer-contact-proof.ts` invokes the actual runtimeUnitPicture and boardStripMarkup at scale1, anchor[32,47]. The unchanged strip translates support by[-16,-16], so v3 support sits[15.9244,29.1404] within the unchanged ellipse centered[16,29.2], extents y25–33.4. All quartet soles overlap the shared contact region; no floating gap is apparent in the generated helper proof. The wide two-person stance extends beyond the central ellipse, as before. This is static actual-helper evidence, not live C02 gameplay or scene integration. Motion GIF and native/source comparisons are review artifacts, not new consumers.

## Decision

Accept this one candidate for integration-owner review on source anatomy, opposite gait, requested contact targets, bounded registration, full-canvas fit, native edge contract and shared-helper contact. Keep it unbound here. Live-game playback and integration-owner approval are not performed by this bounded source task. Do not characterize the completed asset as already live.

Reproduce: run `python docs/art-rebuild-20261019/approved-v5/scifi-boarding-team/blue-support-v3/verify_bundle.py`, then the local vite-node executable on this directory's renderer-contact-proof.ts. Review/source script generates annotations only. All outputs remain in this candidate documentation directory.

## Integration-owner review

The integration owner independently inspected the complete six-source comparison, annotated full v3 canvas and actual shared-helper native proof. Accepted four anatomical legs/boots and reversed blue gait; the new planted boot is below-right of the shield while the other boot is visibly raised. Contact overlaps the unchanged shared shadow without a visible floating gap. The blue step is subtle at native resolution, not claimed strongly readable beyond the evidence. Bound this complete quartet to the existing native catalog only, using the unchanged registration and exporter. Formal export/check succeeded. The original walk-b and rejected v2 remain preserved. Catalog binding does not establish live playback or full scene quality acceptance.

The scope ledger now reads selected frame sources from the existing frame-overrides owner, rather than reporting the old default walk-b master as selected. A failing-then-passing regression covers all four selected poses and missing versioned-source rejection. This projection change adds no independent selection rule.
