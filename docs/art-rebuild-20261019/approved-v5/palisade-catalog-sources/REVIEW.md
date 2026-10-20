> Correction, 2026-10-20 08:55 UTC: the initial diagnostics below incorrectly forced the atlas default [48,91] as an immutable per-cell contact anchor. This is not the existing ordinary-cell contract. The unchanged exporter derives per-cell anchors from actual measured sourceFoot; existing approved stone cells already use calibrated anchors. See calibrated-owner-review/README.md. Straight/corner/breach are now selected at full-canvas92/[2,2] after native/source review; the original undersizing and subpixel-anchor concerns were audit mistakes. Gate remains unbound for its actual camera discrepancy. Original diagnostic files and source bytes are preserved for transparency. No proposed groundline regeneration was executed.

# Palisade catalog candidates: unbound source-layout hold

2026-10-20. Four independent built-in `image_gen.imagegen` calls, each with true transparency requested. No regeneration or post-processing of source pixels. Raw PNG and named master are byte-identical in each audited proposed master directory. Exact prompts and provenance/SHA evidence are alongside each master. No shared catalog, manifest, runtime atlas, exporter, gameplay code, active wall branch, or Git state was modified.

## Decision

**All four remain UNBOUND. Do not count them as integrated or accepted catalog replacements.** Straight/corner/breach are clearly too small under conservative whole-canvas registration. Gate is materially larger than the other candidates and its body is usable in size, but it is markedly more frontal than the family. None of the diagnostic registrations preserves the required [48,91] exactly, so none is a valid drop-in entry.

The original complete 1254-square keyed board, immutable 768-square HD atlas, and approved matte stone-gatehouse source were visually inspected before generation. Repository search found no existing independent masters/raw images for these four identities. The separate border-palisade-fort was excluded.

## Independent-source identities and visual assessment

- Slot6 straight: eight distinct pointed stakes, two rope bands, genuinely straight row, muted wood/dark feet. Identity retained. Slight wood facet variation is present but no mottled surface noise; rope remains more detailed than ideal. Near-frontal view is compatible with the original straight row.
- Slot7 corner: explicit joined V with two straight runs and central foreground return. Not a curved row or crossed barricade. More stakes than original but semantic joined-corner silhouette retained. Rope is continuous, with reduced fine detail at native size.
- Slot8 gate: closed double timber door, two dark ring handles, iron straps, gray stone footings and two blue-gray/pale-cross banners retained. Does not become a stone gatehouse. Symmetric nearly frontal camera and tall narrow posts diverge from original elevated asymmetric three-quarter appearance. Reject for family perspective until genuinely recomposed.
- Slot9 breach: obvious central gap, surviving pointed ends, snapped inward stakes, two diagonal fallen timbers and low gray rubble. Correct damage identity. Timber facets/frayed rope are somewhat finer than intended, though not mottled. Tiny native rendering obscures some rubble separation.

## Exact metrics

All generated canvases are **1254×1254 RGBA**, not the scaled display dimensions. Contact planes were visually assessed in source-pixel coordinates; no alpha crop or bottom-of-canvas proxy was used. The gate contact is a projected ground plane between its two footings, not an opaque point at the center.

| ID | Source foot | Original native alpha128 bbox | Candidate native alpha128 bbox | Diagnostic anchor |
| --- | --- | --- | --- | --- |
| straight | [627,970] | [5,39,91,91] (86×52) | [37,78,59,91] (22×13) | [48,90.79106858054226] |
| corner | [627,1040] | [5,18,91,91] (86×73) | [35,72,61,91] (26×19) | [48,90.88038277511961] |
| gate | [627,1151] | [7,5,89,91] (82×86) | [18,37,78,91] (60×54) | [48,90.82535885167465] |
| breach | [627,1002] | [18,34,91,91] (73×57) | [36,74,60,91] (24×17) | [48,90.77511961722487] |

The original gate baseline slot contains a detached lower spike from the neighboring original board subject, visible in the comparison. Its alpha128 bbox therefore overstates the gate body height; do not interpret82×86 as the clean gate silhouette or claim the candidate gate is undersized from that number alone. No baseline pixels were repaired or cropped.

`proposed-registration.json` records exact scale/origin/sourceFoot values and marks them **diagnostic only**, not production registry entries. Source-foot measurements are approximate physical contact assessments; they were not adjusted to force the catalog anchor. Both densities use the unchanged existing `render_cell` implementation. All four complete source canvases fit; origins satisfy the nonnegative half-logical-pixel lattice. An oversized-canvas negative test was actually run for each and correctly rejected by the existing guard.

All four pass the existing transparency/source-edge guard: edge maximum 0,1,1,1 respectively. Alpha1 residue reaching source edges is preserved. Source visible bodies are near opaque (alpha>=240 pixel counts 681037,545359,888920,478651), but fully opaque255 pixel counts are only 744,682,1299,577. Therefore do not claim literally fully opaque interiors. Native white/black/olive comparison showed no obvious broad translucent fill or halo, but subtle sub255 interiors remain a limitation of these raw outputs.

Footprints remain [2,1], [2,2], [2,2], [2,1]; existing96/192 dimensions, slots6–9, obstructionHeight1 and bottom-anchor semantics are not changed. Production anchor remains [48,91] because nothing was registered. No gameplay placement exists or was invented. No live scene/browser acceptance claimed.

## Bounded genuine source-recomposition proposals (not executed)

Regenerate from the corresponding preserved raw as identity reference using imagegen, with the same complete source canvas, no cropping, no resampling of the current source to simulate new art, and no alpha surgery. Replace the contradictory generic8% bottom margin with explicit **ground-contact plane near96.7% canvas height**, about y1213 on a1254 canvas, while keeping the entire silhouette above y1228 and at least26px clean bottom safety. Aim for lateral silhouette x50..1204 and top safety >=50px. This accommodates roughly92 logical canvas pixels near origin[2,2] without forcing a false contact. Actual outputs must be remeasured; these are composition goals, not preapproved registration coordinates.

1. Straight: redraw the eight-stake row low on canvas, feet near y1213, span x50..1204, tops about y510, retaining the short wide original proportion and exactly two broad rope bands. Simplify rope to large facets. This should recover approximately84×51 native core rather than22×13.
2. Corner: redraw joined V low on canvas, central foreground stake foot near y1213 and receding end feet correctly above it; span x50..1204, tips about y260. Preserve two straight runs and joined bend, not curved geometry. Target approximately84×70 native core rather than26×19.
3. Gate: genuinely redraw in the original elevated three-quarter camera with asymmetric visible depth to posts/footings; closed double door, exactly two ring handles and both blue-gray pale-cross banners. Ground plane near y1213, full banner/footing span x50..1204, top safety about y60. Target approximately84×85 native core. Do not horizontally squeeze or rotate this frontal image as a shortcut.
4. Breach: redraw low on canvas with the rubble/fallen-timber ground plane near y1213, pointed survivor tops about y430, span x110..1120. Preserve a clearly open center, snapped inward timbers and exactly two fallen diagonal timbers. Reduce rope fibers and timber facet noise. Target approximately74×57 native core rather than24×17.

Each future output needs raw preservation, full-source inspection, true alpha/source-edge validation, exact anchor/whole-canvas feasibility, original/native96/192 comparison and white/black/olive review before binding. Current sources remain intact regardless of any future candidate.

## Evidence

- `native-white-black-olive.png`: actual96 and192 previews on all three backgrounds, inspected in full.
- `original-candidate-native96.png`: immutable original slot versus conservative candidate at native96.
- `proposed-registration.json`: exact source paths, SHA256, alpha metrics, diagnostic transforms and guard results.
- Per-master `prompt.txt`, `raw.png`, named master, `source-evidence.json`.

Only the existing render/alpha functions were exercised in an isolated preview script; full exporter plan/write/check, shared regression suite and browser were not run, because these candidates failed the required native-size/perspective quality review and were therefore not selected for production integration.
