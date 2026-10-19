# Fixed-source proportion and camera study

The user's latest feedback identified disproportion. The green ground A is held constant for comparison, not considered user-approved. No new artwork, material edits or image generation were performed. All object source hashes, standing points, bridge centers, terrain cells and gameplay collision/occupancy data remain unchanged.

The current review is `proportion-review/proportion-three-sets.png`, with a separate `proportion-camera-only.png`. Sources retain their whole canvases and original alpha. Dimensions below are logical visible extents measured from alpha>=128; person/tree dimensions are heights, fortress/bridge dimensions are widths.

| Set | Person | Pine | Fortress | Bridge | Road core |
|---|---:|---:|---:|---:|---:|
| S0 baseline |34|90|128|96|15.36|
| P1 balanced |28|76|144|96|22|
| H1 landmark emphasis |34|70|160|96|30|

H1 originally requested32-pixel road core. Root approved30 instead because a full-cell32 width opens disconnected NESW edges. The thin shoulder adds roughly1 logical pixel total. All16 connectivity masks and opposing joins are tested. River occupancy remains two32-pixel columns; water source, width and geometry are unchanged.

All three scale panels use viewBox[-96,0,640,512]. P1 camera-only uses[-24,0,560,448] and the exact P1 sources/scales/positions, yielding1.143× magnification at the same output size. This isolates camera from object scaling.

## Findings and limits

P1 reduces visual crowding while preserving readable figures. Bounding-rectangle checks find no unit-unit overlap in P1, compared with one pair in S0/H1; these are visual rectangle warnings, not opaque-pixel intersection or gameplay collision claims. Some tree-unit overlap remains at the original placements. Moving scenery to solve that would be a separate presentation decision.

H1's larger fortress is clipped at the top of the fixed viewport. This is deliberately retained as evidence, not hidden by shifting the building or camera. P1 fits the baseline viewport; the tighter camera trims about0.06 logical pixel from the fortress's left alpha128 bound, effectively a subpixel boundary contact, recorded in the full report.

Early S1/S2 trials were superseded before delivery and remain diagnostic files only. The final S0/P1/H1 comparison does not imply equal quality or user acceptance. Actual gameplay presentation, shadows and camera behavior remain unverified by this static art study. Full bulk remake remains paused at the style gate.
