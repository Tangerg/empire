# FX12 static cohort and emitter review

## Preserved source identity

Atlas03 row 4 shows three narrow, charcoal-outlined amber heat currents above a stationary row of small dark coals. The new drawings preserve that subject. They are separated wavering strips with transparent space around and between them, without a glow slab, broad haze rectangle, ordinary-fire plume or filled ground plane.

The original final panel has only short remnants. The new fourth phase instead retains long lower replacement ribbons beneath outgoing upper fragments. This resolves the obvious empty/tiny-remnant reset while retaining the source identity.

## Fixed emitter and actual drawing variation

The coal bed's alpha-128 bounds are:

- heat-curl-a: [199,1074,1106,1165]
- heat-curl-b: [199,1075,1107,1167]
- heat-split: [200,1075,1107,1167]
- heat-renewal: [200,1075,1108,1169]

The largest bounds spread is 4 source pixels, or 0.089 logical pixels. The coal row is visually stationary at 32 and 64 pixels. Its shading and contour are not byte-identical. No per-frame shift or fit was used to achieve this registration.

The pointed ribbon roots have slightly greater drawing variation above that emitter. Bottom-tip Y spreads are 13, 14 and 1 source pixels for left, center and right; maximum 14 is 0.313 logical pixels. Tip-X spreads are approximately 5.53, 5.53 and 3.64 source pixels. The transparent gap over the coals remains present. The first split attempt, which moved the roots and closed that gap, and the first renewal attempt, which moved the entire coal row down, were rejected and preserved.

## Cohort motion and seam

The initial upper tips advance upward from [314,150,352] in phase 1 to [267,134,335] in phase 2. In phase 3 the left column stays continuous; the center and right upper portions detach while substantial lower bodies persist.

From split to renewal, the detached center portion changes from Y122–429 to Y74–233, and the right portion from Y334–548 to Y315–493. Both move upward and become smaller, though the right fragment advances much less. The continuing lower center grows upward from top Y439 to Y247; the right lower body grows from Y545 to Y502. The left column gains its own detached older tip in renewal.

Across 4→1, the lower replacement tips continue upward:

- Left: Y394 → Y314, an 80-pixel advance or 1.79 logical pixels
- Center: Y247 → Y150, a 97-pixel advance or 2.17 logical pixels
- Right: Y502 → Y352, a 150-pixel advance or 3.35 logical pixels

The three old upper remnants retire at the wrap. They are not the same pieces moving downward into the next continuous columns. The continuing lower replacements supply the next phase's full ribbons, so no frame becomes empty or a tiny ember-only state.

## Limits of the static evidence

The wrap retires three fragments and reconnects three growing columns within one frame interval. It may still read as a reconnection pop at 12 fps, especially on the right with its larger growth step. Interior crests and widths are independently authored shapes; they cannot be treated as individually tracked material particles. The samples establish rising upper tips and explicit split/renewal topology, not a conserved flow field for every bend.

Whole-source alpha masses are 195883, 201358, 192935 and 187050. The largest adjacent drop is about 4.2%, and renewal→first rises about 4.7%. The cycle keeps comparable visual weight without a programmatic alpha fade. At native 32 pixels, some split gaps are subtle; they are clearer at 64 pixels. Three heat lanes and the coal bed remain readable on the six tested backgrounds.

## Checks and unobserved playback

Full-source black/white, native 32/64 plus zoom, full 1–2–3–4 and seam 3–4–1–2 were inspected. Source perimeter alpha is 1/0/0/0; the original alpha-1 residue produces at most one RGB channel level of perimeter difference on black/white. No cleanup was applied. Every final cell has alpha-zero 2-pixel logical padding.

All 28 APNGs and 112 decoded frames were verified. Four equal 1/12-second frames repeat indefinitely, with a 1/3-second cycle, no final hold and no 420 ms removal rule. The alternate diagnostics run at 3 fps.

Eight uninterrupted cycles were not observed in a browser. The parent reported a known browser/GPU block, and this worker did not repeat escalation. There is no authored scene consumer. This is offline catalog artwork and static QA, not live gameplay, deployment evidence or a claim of verified smooth playback.
