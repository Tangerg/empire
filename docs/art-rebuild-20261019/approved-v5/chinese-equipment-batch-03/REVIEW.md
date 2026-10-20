# C03 equipment batch 03

Four independent built-in image_gen.imagegen transparent sources, with untouched raw/master PNGs and isolated native48 exporter previews. No shared registry, progress, manifest, runtime, code or Git edits were performed by this worker.

## Scope audit

Canonical manifest order after c03-equip-2bc4ddf7 yields:
- c03-equip-2be16bd2 架桥营: short three-plank bridge panel and rope coil.
- c03-equip-3d86046f 火铳手: primitive hand cannon on pole handle and powder pouch.
- c03-equip-3f4a6e1d 密察司缉事: sheathed saber and blank identification tablet.
- c03-equip-3f6fea2f 潜渡凿船手: mallet, broad chisel and rope loop.

At selection, no approved-v5 source directories or active reservations existed for these topics. 架桥营 had an older remake-20261019 registration, which is not approved-v5 completion. It is therefore intentionally included; the older source is untouched. The other three had no source overrides. Canonical IDs and role names were checked against package manifest, canonical-topic-manifest, topic catalog and asset scope. Candidate-03 README defines engineering as terrain modification/siege/dike repair and firearms as formation-breaking with limited ammunition; the scope identifies investigation as strategy and the naval sapper as naval. These are role-based visual interpretations, not new gameplay mechanics or claims of exact historical loadouts.

The full original equipment atlas was visually inspected before generation. The approved broad-matte token was used only for style. Legacy native images were preserved and hashed: bridge panel is semantically relevant; 火铳手 shows a ladder, 密察司缉事 a straw-hatted shield kit, and 潜渡凿船手 a map. Those three are explicit semantic reconstructions, not exact legacy-slot recovery.

## Visual review and shortcomings

Reviewed complete generated sources, full-source white/black/olive composites, and actual native48 white/black/olive previews. Four groups remain distinguishable, fully contained and readable at native size. No backing plate, cast shadow, checkerboard, white halo, stippled mosaic or noisy surface facets were observed. Broad matte planes and heavy contours dominate.

The output does not perfectly achieve literal two-tone surfaces: metal has broad bevel accents; mallet wood and cannon handle contain a few elongated secondary facets. Bridge rope has many twist divisions, and the mallet has two iron reinforcement bands not specified in the prompt. These details remain coherent at native48 rather than becoming mottled noise. The identification tablet has no lettering, so authority/investigator specificity depends on the paired saber and canonical label. The bridge panel can also read as a miniature raft; it intentionally has no water, railings or ground. The naval tools alone do not identify underwater use without the canonical label. These limitations are disclosed rather than treated as exact object recovery.

## Integrity and export verification

- Each topic has raw.png, master.png, exact prompt.txt, source-evidence.json, and alpha-qa.json in its new approved-v5 equipment directory.
- All four raw/master files are byte-identical 1254x1254 RGBA PNGs. Provenance records built-in source filenames, SHA-256 and reference hashes.
- Existing export_overrides.plan passes all four in isolation. Geometry is 48x48, anchor [24,24], whole authored canvas contained to 44x44 at [2,2], padding2, LANCZOS. No tight-alpha crop, cleaning, binarization, redraw, HD substitution or source alteration.
- Three source perimeters have near-transparent alpha1 pixels (bridge 9, investigator 60, naval tools 30), within the unchanged existing edgeMaxAlpha <=1 contract. Cannon source perimeter is zero. Every native48 perimeter is entirely alpha0.
- Evidence: batch-alpha-integrity-qa.json, export-plan-qa.json, legacy-native-enlarged-review.png, full-source-review-white-black-olive.png, native-review-white-black-olive.png.

integration-candidates.json is the isolated handoff. Registration, production export and live-game acceptance remain the parent's work and are not claimed complete.
