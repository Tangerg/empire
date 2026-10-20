# C03 equipment batch 02

Four next canonical equipment candidates, independently generated using built-in image_gen.imagegen with actual transparent output. Only this isolated review directory and the four new approved-v5 equipment master directories were written. No shared registration, runtime, manifest, code, progress, or Git edits.

## Candidates

- c03-equip-15c058cc 神臂弩: heavy crossbow and two oversized bolts.
- c03-equip-222078e0 艨艟: paddle, iron anchor, and rope loop.
- c03-equip-25f91749 楼船兵: broad red boarding shield and two-pronged boarding hook.
- c03-equip-2bc4ddf7 火油防备队: sand bucket and folded smothering blanket.

## Semantic audit and limitations

Canonical IDs and labels were checked in canonical-topic-manifest.json, the package manifest, 02-asset-scope.md, and 07-topic-catalog.md. Candidate-03 README categorizes crossbow roles as ranged, and naval roles as water movement/boarding/fire attack. ART-DIRECTION identifies Chinese military equipment vocabulary. The full original equipment atlas was visually reviewed before generation, alongside the approved broad-matte token style reference. These are canonical-role reconstructions, not exact atlas-slot recovery and not claims of historically documented loadouts or additional gameplay mechanics. The fire-defense sand bucket/blanket is a visual interpretation of the defensive label; the catalog does not specify its individual tools.

Legacy runtime images are visibly unreliable as identity references: 神臂弩 currently shows a bow/quiver, 艨艟 a saddle, 楼船兵 an isolated paddle, and 火油防备队 armor. They were preserved and hashed, not overwritten. See legacy-native-enlarged-review.png.

## Visual review

Full generated sources and native48 exports were reviewed over white, black, and olive backgrounds. All silhouettes remain contained and distinct at native size, with calm broad painted planes and strong dark contours. No mosaic/stipple, grain, checkerboard, cast-shadow backing or glossy photographic rendering is present. Generated iron edges have broad pale bevel accents, and wood and blanket have several broad facets rather than exactly two mathematical colors. The shield has extra simple rivets and angular wood accents. These deviations remain legible and do not recreate the rejected surface noise. Native48 crossbow string and hook tips are thin but remain visible; the shield and anchor dominate their respective silhouettes. Blanket dampness is semantic, not unambiguously visible.

## Integrity and export verification

- Raw and master files are byte-identical 1254x1254 RGBA generated PNGs, with SHA-256, exact prompts, reference hashes, and provenance per topic.
- Genuine alpha is preserved, including internal gaps. Source transparency counts are in batch-alpha-integrity-qa.json. Alpha is not binarized; native generated near-opaque/intermediate pixels are unchanged.
- Three source perimeters are fully transparent. Fire-defense source retains five perimeter pixels at alpha 1, within the unchanged existing ≤1 contract; no cleanup was performed. Every native48 perimeter is fully alpha 0.
- Existing export_overrides.plan passes all four candidates in isolation. Canonical geometry remains 48x48, anchor [24,24], entire authored canvas contained into 44x44 at [2,2] with padding 2 and LANCZOS. No tight alpha crop, redraw, removal, or HD export.
- Full resolution three-background evidence: full-source-review-white-black-olive.png. Native48: native-review-white-black-olive.png. Export evidence: export-plan-qa.json.

integration-candidates.json is the parent integration handoff, not an active shared registry. Parent owns registration, runtime export and in-game verification; those are not claimed complete here.
