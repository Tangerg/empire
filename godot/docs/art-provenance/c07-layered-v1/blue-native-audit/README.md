# C07 blue stand — isolated material checkpoint

2026-11-01. Production `c01-07.json` is unchanged. The new `c01-07-prop-preview.json` is runtimeReady=false and deliberately incomplete: one independent blue stand, clean-ground candidate and **legacy map-structure placeholders**. All other authored objects are absent. This is not a complete scene, gameplay, carryover or victory acceptance.

The canonical initial state is cloned only to place Torren in two static poses; no engine command or real save is used. Native renderer digest unchanged, diagnostics 0. At (56,11), the real opaque actor pixel behind source (910,170) is hidden by roof material; the original painted baseline exposes that same pixel (negative control). At (56,13), the opaque actor pixel at source (907,200) remains visible/pickable in front. Candidate assertions require both actual actor alpha and correct opposite depth results, not an empty-point success.

Two measured wooden feet fit the original (900,209)/(915,204) under a uniform scale, maximum axial residual 0.2673 source pixels. The lower solid panel is retained; it is not reinterpreted as an open walk-through shelter. Existing anatomical sole checks do not establish a foot collision with the wood; hidden rear footprint/all character motions remain unaccepted. Original shape height is about 57 source pixels; candidate is about 60 source pixels including thin silhouette tips, no nonuniform stretch.

Native screenshots outside the repo: `c07-static-preview/north-pose-56-11-painted.png`, `blue-pose-56-11-candidate-labeled.png`, `blue-pose-56-13-candidate-labeled.png`. Same 1.25 camera and 1180x812 viewport. Gray top area is this unclamped diagnostic camera, not the default opening camera. Material direction accepted for continuation by visual review, not final all-level art acceptance.

Fence v2 is retained only as rejected provenance: all three frozen openings are opaque and original upper-rail witness is empty. No fence is bound. Next isolated investigation uses independent posts/beams with original geometric landmarks, not repeated whole-fence scaling/generation.
