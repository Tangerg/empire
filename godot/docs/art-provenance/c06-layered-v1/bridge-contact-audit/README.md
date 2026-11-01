# C06 bridge contact audit — unresolved preview blocker

2026-11-01. Production still uses its historical painted scene. The 95-object
assembly remains unbound and `runtimeReady=false`. This audit does not certify
new gameplay, bridge correctness, or visual acceptance.

## Evidence and contract

- All 40 canonical bridge cells have elevation 0. Bridge cost is 1 for foot,
  mounted and heavy movement; the ballista cannot be omitted from this review.
- Initial-state-only native presentation sampled 14 resolved appearances,
  four authored frames and both facings. No engine command, AI turn, real save
  access or campaign advancement was performed; the initial state remained
  unchanged. Separate static actor screenshots have an explicit fixture label.
- Anatomical review distinguishes actual boot soles / wheel rims from weapons,
  clothes and raised boots. `knight` here resolves to a walking armored person,
  not a mounted horse. Candidate contact ROIs are physical pixels local to each
  actual texture frame; their anchors and source scales are recorded.
- Nonhero short witnesses: 75 lines / 1882 source pixels independently checked
  against original PNG alpha >80, current ROI bounds and actual lower contour.
  This is a diagnostic sampling threshold, not an engine alpha change. Four
  heroes add 16 frames / 24 manually inspected boot ROIs. Ambiguous toe-up walking
  heels remain uncertain, never silently converted into full planted soles.
- Projection covers 8160 anatomical part / facing cases over the 40 cells.
  `all40-summary.json` compares them with **approximate historical deck quads**.
  Positive distance is a material/shore inspection candidate, not an automatic
  final failure. Bridgehead road and visible curb must be judged separately.

The old full lower-four-source-pixel envelope was conservative, not anatomical.
Its approximately +/-21.5 extent for the ballista comes mainly from the operator's
actual rear boot, not the shaft. Original pixel boundaries extend slightly beyond
that half-pixel scan quantization. Do not shrink this witness to manufacture a pass.

## Observed defects and rejected repair

Same-camera static actors exposed support candidates in both historical painted
and layered preview scenes, predating the new rails. Subsequent original-pixel
review distinguishes seven actual water/underside/vertical-post contacts from two
false or unresolved candidates; the approximate quad alone is not a verdict:

- West (9,11), (11,11): water or underside rather than flat walking surface.
- Central (24,14), (27,14): water or arch side; central (25,12): pillar face.
- East (40,13): pillar face; east (45,15): water.
- West (10,10): bank rock/snow, with height unresolved. The earlier wording
  “inadequate support” was too strong without confirming rock elevation.
- East (42,13): actual wood/edge beam. Do not repair this quad false positive.

Anatomical idle candidates reach about 13.6 source pixels beyond the old
approximate deck contour, primarily due to the ballista operator's boot.

A whole-deck uniform widening candidate was **rejected**: it also painted deck
across neighboring canonical water-cell centers. Terrain, actor coordinates,
movement classes and map rules must remain unchanged. Repair must reconcile
local deck/bridgehead support, rail endpoints and lamp physical bases, not fill
river channels or merely test a single centered foot.

West (7,14) is concealed by the new foreground lamp, with its selectable cue
retained. Concealment is proven; physical base/sole intersection is **not** yet
proven. The three visible base feet project near (249,444), (264,450), (277,444),
while the canonical actor contact is (260,436). Do not move a lamp just because
correct foreground occlusion hides an actor that the old flat painting overlaid.

## Remaining work

Inspect actual floor plus bank pixels, retain neighboring water negative controls,
resolve localized support without unnatural bridge shape, then align independent
rail/lamp geometry and re-render the same actor poses. Full-body occlusion,
interaction and later normal-AI C06 gameplay remain separate acceptance stages.
No failing historical check was deleted or loosened by this documentation commit.

## Later bounded candidates — not adopted

The local row-envelope candidate `exec-9c2e779d-a411-489b-8bbc-91b8f941a657`
was rejected visually: straight spans became curved or kinked, especially the
stone bridge. The final natural-platform attempt
`exec-77e431eb-084b-4dbb-aaaa-23b868ded4bc` retained straighter spans but did not
cover the seven verified physical contacts consistently; protruding platforms
also read as separate attachments rather than coherent construction. This
support candidate is stopped, not accepted. Neither image is bound to the
preview or production ground; canonical terrain and movement remain unchanged.

Original material crops, seven-point diagnostic comparison and rejected output
bytes are retained in recovery Library item
`libfile_17777e1530488191bd13f04685f4661f` version23. These are static artwork
checks, not additional gameplay or a successful bridge acceptance.
