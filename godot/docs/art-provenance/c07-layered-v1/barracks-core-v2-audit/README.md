# C07 barracks core v2: bounded native depth checkpoint

2026-11-01. **UNBOUND preview only**, canonical initial actor clones; no engine commands, campaign carryover, real save, AI or gameplay. Production c01-07 scene and rules are unchanged. This is main core plus the separate blue stand, with canopy/cargo and other city objects still absent/legacy placeholders. The complete62-cell foundation responsibility matrix is NOT accepted.

## Material/contact result

v1 fit left precise source contact was empty and door point nearly empty. One localized imagegen correction produced v2 (1609x977). Actual measured stone-foot interior points573932/902846/1136843 have alpha250/253/252; these are structural contacts, one a door jamb, not three outer corners. Minimax uniform scale.15789473684210353, translation912.0526315789486/39.36842105263315 gives max axial residual.473684 source pixel. Old fixed source points retain A61/252/252; left is an antialiased edge. Its distance beyond solid edge is.157895 source pixel. This is NOT reported as three exact solid contacts. Original manual material location has about1source-pixel boundary uncertainty; neither samples nor a new tolerance were adjusted to force a pass.

The localized request did not produce bit-identical untouched pixels: width shrank1asset pixel and maximum upper contour displacement2.236asset pixels; several upper texture patches best-align1px left. No obvious roof structural redesign. This bounded result is retained, not repeatedly regenerated.

The lower wall/threshold/stone-base domain122..1533 was re-inspected independently on v2; a1412-column actual solid/ink edge was recorded and reduced to85 nodes. Roof-only outer columns use explicitly documented support-side extrapolation, NOT lowest-roof-alpha-as-ground. Catalog depthStrips cover the entire whole PNG without discarding any alpha. The276 strips preserve the local sloped/stepped ground-front relation rather than copying the old paved-court front1055206. **Performance is unaccepted**; this large depth-piece count requires later proven compression/batching, not a production-performance pass.

## Final native checks

External fixture rebuilt0warnings/0errors; positive exits0, deliberately removed-strips single-front negative exits1. Canonical digest unchanged, rendering issues0.

- Original failing pose(60,12): actual head region270/270 pickable (old190/270 hidden);34 genuinely overlapping main-core pixels remain pickable. Thus the new core is not simply absent at every old sample, though much old cargo is intentionally not present yet.
- Local side/front(60,11):355/355 shared material pixels pickable. Front(68,12):65/65.
- West-side rear(57,8):122/122 shared pixels hidden. Rear(63,6):518/518 hidden,83body pixels still visible and cue pickable; rear(64,6):585/585 hidden,16body pixels visible and cue pickable.
- Right-side(73,9) had no shared material alpha; reported as nonoverlap, not a depth positive.
- Forcing the actual rear core pieces behind the actor reveals5/5 sampled hidden pixels (negative control).
- Removing local strips and returning to one whole-building front makes(60,11)355/355 pixels wrongly hidden and fails the assertion. The test therefore rejects an overall-back/front shortcut.
- Actual door material chooses canonical Object(65,14). Roof point102070 and paved point1044184 choose Ground, not the site. Ground hit kind is not a walkability claim: canonical foundation/approach coverage still needs the full matrix, and the building is not moved to its abstract capture coordinate.

Actual screenshots: c07-static-preview/barracks-core-front-v2.png, side-v2.png, behind-v2.png (diagnostic1.25 camera) and closeup-v2.png (zoom4); same-pose old closeup is fence-closeup-painted-v2.png. Parent visually reviewed front/back and accepted this static candidate for continuing canopy/cargo integration, not final city/62# coverage. Material/ground sharpness and contact-shadow coherence remain scene-integration debt.

## Reproduction boundary

Use recovered c07-static-preview project with checkout assets/data symlinks, referenced current Empire DLLs; build Probe.csproj and run BarracksCoreProbe.tscn headlessly. Default is c01-07-barracks-preview.json. C07_BARRACKS_SCENE=res://bad-single-front-scene.json selects the isolated deliberately-failing mutant. These switches and actor clones are only external fixtures. GUI screenshots use the independent terminal tab, never the paused real C06 window. Do not count this as resumed gameplay.
