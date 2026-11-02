# Original 19-cell responsibility — independent ground / alpha projection

**Ready: 11 ground-area cells, 7 elevated-material-only cells, 1 without substantive candidate material. This is not 19-cell physical completion.**

| Cell | Terrain | NEW base area px² | Candidate A≥200 area px² | Classification |
|---|---|---:|---:|---|
| 21,23 | # | 0.000000 | 0.597300 | projected-material-only-no-ground-domain |
| 22,23 | # | 0.000000 | 162.324775 | projected-material-only-no-ground-domain |
| 23,23 | # | 0.000000 | 256.000000 | projected-material-only-no-ground-domain |
| 24,23 | # | 0.000000 | 256.000000 | projected-material-only-no-ground-domain |
| 25,23 | # | 0.000000 | 253.652600 | projected-material-only-no-ground-domain |
| 21,24 | # | 0.000000 | 3.260850 | projected-material-only-no-ground-domain |
| 22,24 | # | 0.000000 | 195.721200 | projected-material-only-no-ground-domain |
| 23,24 | # | 1.542693 | 256.000000 | new-ground-domain-positive-area |
| 24,24 | # | 45.163826 | 256.000000 | new-ground-domain-positive-area |
| 25,24 | # | 0.012103 | 256.000000 | new-ground-domain-positive-area |
| 21,25 | # | 0.000000 | 0.000000 | no-candidate-substantive-material |
| 22,25 | # | 72.356235 | 161.018925 | new-ground-domain-positive-area |
| 23,25 | # | 220.202560 | 256.000000 | new-ground-domain-positive-area |
| 24,25 | # | 256.000000 | 256.000000 | new-ground-domain-positive-area |
| 25,25 | # | 165.659008 | 256.000000 | new-ground-domain-positive-area |
| 22,26 | # | 4.831137 | 5.180825 | new-ground-domain-positive-area |
| 23,26 | # | 133.385338 | 134.893650 | new-ground-domain-positive-area |
| 24,26 | # | 175.394450 | 177.623150 | new-ground-domain-positive-area |
| 25,26 | # | 70.692512 | 72.950325 | new-ground-domain-positive-area |

## Eight cells without new ground area

All eight remain canonical `#`: (21,23), (22,23), (23,23), (24,23), (25,23), (21,24), (22,24), (21,25).

(21,23) and (21,24) have only 0.597300 and 3.260850 px² at A≥200; positive projection does not mean an entire cell is visually filled. (21,25) has zero A≥9 area and merely 0.004250 px² of A1–8 trace.

Original inspection shows elevated roof/gable material or foreground vegetation in these cells, not certified house foundation. The old west-middle-house-east-wall audit overlaps (21,23)/(22,23), but its polygon alone does not prove a true wall foot. No ownership is transferred on that basis.

Selected blue full NEW base starts at source Y421.6; these eight cells end at or before Y416. Its ground domain therefore cannot take over them, even where the original blue roof is visible.

The remaining physical responsibility is explicit and unresolved. No arbitrary stone infill, invisible base reconstruction, or claim of 19-cell completion is made.

## Evidence / reproduction

- `rear-house-v1-19-cell-responsibility-projection.json` — all 19 rows, exact projected texel-box areas at alpha 1/9/128/200/245, source/candidate center RGBA, original observations and ownership limits.
- `original-19-cell-responsibility-grid.png` — unchanged source crop (328,352)–(440,448), nearest-neighbor ×8 with cell labels.
- `verify-rear-house-v1-responsibility-projection.py` — reads frozen registration / fixed whole-base report and source inputs; writes only this review directory.

This is unfiltered static material projection, not rendered scene visibility, a foundation mask or a native gameplay check.
