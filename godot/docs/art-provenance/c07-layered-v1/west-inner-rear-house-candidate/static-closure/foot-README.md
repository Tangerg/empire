# Rear annex v1 — bounded ground-domain review

**Ready: no conflict found for the fixed candidate and explicitly NEW hidden closure.**

- Supplied 759 stone-foot samples remain unchanged; actual alpha 245–255. The near reference at asset (654,1068) is the opaque dark stone outline, not a pale interior material pixel. Candidate size 1374×1145.
- Uniform registration .085, anchor (654,1068) → source (384,430). Visible endpoints L=(356.46,410.11), R=(420.89,414.955). NEW hidden corner L+R−near=(393.35,395.065); not certified original hidden masonry.
- Full finite base area 1159.1681625 source px². Positive-area intersection only with 13 canonical blocked (#) cells; zero passable/outside cell area.
- Against selected blue house's complete explicit NEW base: no boundary intersection, no vertices contained either way; minimum distance 8.834625063 source px.
- Local legal bounds x20–28, y22–28: 18 legal cells, 40 directed adjacent edges, frozen walk-a/walk-b contours and applicable facings, 10,500 continuous sole segments. No boundary or endpoint-inside intersection; minimum clearance 7.908089937 px. Closest: walk-b west (21,26)→(20,26), frame pixel (34,553), actor (359.384608,422.2692316) versus stone (363.94,415.805).

## Old 19-cell responsibility is not a physical mask

`luojia-geometry.json` → `blockedPolygons[id=west-inner-back-house-foundation]` contains 19 cells. Eleven have positive new base area; these eight do not: (21,23), (22,23), (23,23), (24,23), (25,23), (21,24), (22,24), (21,25). New base also enters blocked (26,25), (26,26), outside the old envelope. This is **not** 19-cell completion and is not automatically a defect: source roof/wall projection, trees and courtyard conceal much of the old area. No unverified original foundation or neighboring ownership is inferred.

## Reproduce / evidence

- `python c07-readonly-review/west-inner-rear-house-review/verify-rear-house-v1-whole-base.py`
- `rear-house-v1-whole-base-result.json`: complete fixed feet, full polygon, clipped cell areas, exact separation and closest walk witness, input SHA256s.
- `registration-frozen-input.json`: supplied unchanged registration.
- `candidate-stone-band-inspection.png`: crop of manually bounded lower masonry, not full-image alpha extremum.
- `original-rear-house-context.png`: original source crop (320,315)–(455,455), ×6; partly obscured building, not evidence of hidden foundation.

Only source/candidate pixel inspection and static mathematics. No GUI, engine commands, action, actual saves, or production changes. Ground separation does not certify eaves/depth/hidden 3D solids, all character boots, gameplay, or complete old responsibility recovery.
