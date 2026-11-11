# Independent gravekeeper fall source review

**Accepted: source art and offline registration at one presentation scale of 0.1.**
No concrete blocking visual defect or missing source-packet proof remains in the reviewed candidate. This is not production integration, native playback, legal-gameplay, full-gate or performance acceptance.

Reviewed against repository baseline `ea0eb0513e48ed650c2c04d259bfc4baee7f3021`.

- Original source SHA256: `a60966fbdd436e675060f64baffdb22e9ce957c0e3c801eabb83548487e8991e`.
- Accepted 2154×405 atlas SHA256: `b6467bfb6ad3c5c6a09332034798a0afc2478962817fa3060d44917c2b364326`.
- Accepted atlas raw RGBA SHA256: `7df08dee73e18372913d6579336f80391186385f4305859d2a7d3dfe8eedb304`.

## Actual visual inspection

The reviewer opened the original official gait, accepted breathing/casting atlases, accepted recoil source and both-facing recoil contact sheet. The new original transparent source, neutral-backed source, extracted atlas, both-facing native-size contact sheet and its 3× nearest-neighbor enlargement were opened and inspected. Individual diagnostic images were inspected through idle → buckle → kneel → collapse → contact → settle → still → fade, and through the separately labelled recoil-onset/peak handoff. This review is based on those actual pixels, not only image metadata.

The grey swept hair, beard, brows, blue-grey scarf, dark coat, cream sleeves, belt, leather pouches, hanging silver flask and brown boots preserve the established compact old keeper. The anatomical right hand retains a complete wooden-shaft pointed steel digging shovel. The anatomical left hand remains attached to the lantern's top ring. The expected handedness mirrors with the facing. No duplicate limbs, extra hands, duplicated tools, detached equipment, cropped blade or missing lantern was found. Warm illumination stays inside the lantern. There is no blood or added impact/magic effect.

All six pictures contain real anatomical changes. The first frame bends the standing knees and folds the torso; the second drops onto one knee; the third lowers both knees and rolls the pelvis; the fourth brings the hip and supported arm/tool down; the fifth lowers the torso further; the sixth rolls the head onto the bent sleeve and closes into a supported resting pose. Knee/hip, shoulder, elbow, wrist and face relationships change. These are not rigidly rotated, translated, squashed or faded copies of one picture. The final frame reads as a resting body, not a new attempt to stand or a body suspended above the support plane.

The largest discrete change is collapse → contact: the shovel goes from an upward screen-left diagonal to the blade-right near-horizontal resting position. In the source and native-size handoff it reads as the right arm lowering the tool while the body falls sideways. It is not a floating replacement or duplicate. Final shovel and lantern rest with the body on the implied three-quarter ground plane; hands still meet their shaft and ring. Fine fingers are naturally subpixel at native size, so attachment detail was checked on the source as well.

## Registration and finite timing

One scale of 0.1 applies to all six original crops. The standing-buckle silhouette matches the established keeper's head/tool size in the native-size comparison, and the lower/wider later silhouettes follow anatomical collapse rather than per-pose fitting. Both facings retain readable identity and the same contact registration. No visible size jump was found in the inspected idle/recoil-to-buckle handoffs.

Accepted source anchors are `(164,562)`, `(446,562)`, `(738,562)`, `(1023,553)`, `(1411,553)`, `(1832,553)`. The first three use the shoe/knee support line; the last three use the side/hip support line. The root is not recentered to each rectangular crop. The head travels forward as the body falls. First-three lowest nonzero-alpha extents are 0.9–1.0 native pixels below the logical anchor. In the final three they are 2.8/3.2/3.6 pixels below, because the foreground shovel projects lower in three-quarter ground-plane depth. Those lowest tool pixels are not being misidentified as the body support line. The registered body does not visibly float or jump at this support change.

The six intervals are 60/70/90/110/110/180 ms, totaling 620 ms. The final still drawing remains fully opaque for 180 ms. The offline diagnostic's following 160 ms uses only that same still geometry with decreasing alpha; no new pose or movement is introduced before fading. All 26 lossless diagnostic image areas were independently reconstructed exactly from the accepted source crops/reference pixels, anchors, common scale and recorded alpha. The nonlooping GIF has the matching 26 frames and 3310 ms combined diagnostic duration. Individual diagnostic images and GIF timing metadata were reviewed; no continuous real-time playback or native gameplay claim is made.

## Independent pixel verification

`../verify-source.py` independently reconstructs the six components with separate radius-three Euclidean binary dilations. It does not call the artist's extraction script or reuse its global distance-transform ownership result.

The check passes:

- The preserved 2172×724 source is byte-identical to the available original image-tool output. The exact prompt and four reference checksums match the provenance record.
- Exactly six significant alpha components yield 341,766 retained original nonzero-alpha RGBA pixels.
- Only 187,405 detached pixels are discarded, with maximum alpha 8/255. Every source pixel above alpha 8 is retained. Of the discarded pixels, 183,882 have alpha 1/255.
- All six standalone PNGs and the whole packed atlas equal the independently reconstructed original RGBA, including exactly twelve transparent pixels on every side of each crop. No retained pixel is recolored, repainted, resampled, rotated, warped or alpha-modified.
- Source-order rectangles, anchors, shared scale, declared timing and fade policy match the accepted packet.
- A visible one-channel one-bit mutation fails the equality guard independently of file checksums.
- All 26 offline diagnostic image areas match an independent render; both contact magnifications are exact nearest-neighbor copies; GIF frame counts/durations and nonlooping behavior match their declared sequence.

Reproduce from this archived verification directory with:

```
python ../verify-source.py --report independent-source-verification.json
```

The JSON contains exact bounds, anchors, extents, pixel counts and hashes. If the original temporary generation path is unavailable on another machine, the source packet remains verifiable; the report explicitly records whether that additional original-path comparison was available.

## Remaining boundaries

The artist's packet includes originals, exact prompt/provenance, extraction metadata, the atlas, lossless both-facing diagnostics, the offline GIF and a reproducible verifier. No further source-art change is requested.

Production manifest/clip/binding values and live atlas bytes still require integration verification. Actual event ownership, death timing, crowding/feedback occlusion, legal native captures, saved outcomes, complete checks and performance remain for the parent task. No production files, build outputs, imports or Git state were modified by this reviewer. During the original review only independent files under the QA review directory were written. This archived copy and its verifier were subsequently packaged under `docs/art-gravekeeper-fall-20261111`; that packaging did not change production source or data.
