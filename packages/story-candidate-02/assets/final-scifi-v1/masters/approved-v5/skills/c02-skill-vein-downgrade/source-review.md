# c02-skill-vein-downgrade source review

## Verified identity and contract

Canonical label: 星脉降权. Existing C02 skill contract is one static 48×48 PNG, center anchor [24,24], with no HD entry. No authored deployment is inferred from catalog registration.

The actual original 48px runtime was inspected first and visually matched to skills-board-01.png, row 2, column 3 (one-based). Exact complete subject crop: [632, 319, 935, 622]. Both the complete board and exact slot are retained. Topic catalog corroborates the identity; no sorted final-manifest index was used.

No label/pixel conflict was found. The Chinese label establishes catalog identity; the verified original image establishes the complete functional motif. No extra narrative meaning is invented.

The original board has an opaque saturated magenta field. The legacy runtime was produced by the old crop/chroma/despill workflow and has softened colored boundaries; the paired-arm original particularly shows pale colored fringe. Intentional purple parts in the planet and downgrade motifs are not mislabeled as contamination. The verified grid crop contains the complete intended motif without observable clipping or neighboring-card bleed. Original board and runtime bytes are preserved; no old keyed pixels are reused in the generated master.

The read-only build_final_scifi_v1.py chroma() function invokes remove_chroma_key.py, then additionally removes saturated magenta pixels; fit_transparent() crops to alpha bounds. This is historical provenance only. New previews use whole-canvas contain and preserve generated alpha.

The original downgrade identity is retained: one large vertical downward cyan arrow, deep graphite backing, exactly eight cyan circular circuit terminals on four paths per side, and jagged purple fractured material behind it. Purple remains intentionally present in the source motif; it is not treated as chroma contamination to erase. Broad arrow bevel planes replace the original glossy diagonal stripes. No extra arrow, padlock, minus sign or device is invented.

## Generation and review

Exactly one built-in image_gen call used transparent_background=true for this topic. Its returned 1254×1254 RGBA bytes are preserved unchanged in raw-native-v1.png, raw.png and master.png. There were no failed or rejected image-generation attempts. Request, prompt, local reference copies, reference hashes, authority hashes and generated-path provenance are retained. No generated image was cropped, repainted, recolored, sharpened, keyed or cleaned. The source crop and narrow-gap diagnostic crop are reference/QA artifacts only and never replace the generated source.

The full native source, full native white composite, source512 black/olive composites, four-topic three-background contact sheet and actual 48px white/black/olive strip were inspected. Dark outlines are thick and stable, material planes are broad, and energy graphics remain bounded. All three full-resolution background composites are also preserved for independent parent review.

At actual 48px, the downward arrow and branching terminals remain immediately recognizable on white, black and olive. Source512 composites and full-native white inspection show bounded opaque cyan routes over dark purple fractures with no broad haze. The narrowest visible source margin is 34px, below the prompt target but unclipped. The complete-canvas output has a fully transparent 2px border.

## Measured transparency and export preview

Source alpha range is [0, 255]; maximum source perimeter alpha is 1, with 0 perimeter samples at alpha 2 or higher. The allowed alpha 1 residue, where present, is kept unchanged and shows no visible matte haze in composites. Near-opaque source pixel count at alpha 240 or higher: 778338. Supplemental 8px erosion reports minimum alpha 251 and 0 samples below 240. This diagnostic is not an independent acceptance gate and is explained above where needed.

The complete source canvas is fitted using LANCZOS into 44×44, centered on 48×48 with exactly 2px padding. Source bounds are never inferred from alpha. The entire final 2px border has maximum alpha 0. The 6× nearest-neighbor view is diagnostic only. master.png remains byte-identical to raw generation.

## Handoff

Source-ready means ready for parent validation/binding at packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/skills/c02-skill-vein-downgrade/master.png. It does not change formal category counts or claim runtime/live-game integration. This worker made no repository, main, selector, gameplay, manifest or Git edits. All outputs are confined to the four assigned topic directories. Proposed override metadata is included for the sole integrator.
