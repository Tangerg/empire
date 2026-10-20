# C01-STYLE-BORDER-AUTUMN source review

## Outcome and scope

One built-in image_gen call produced an opaque regional visual-reference plate. Selected raw, native and master are byte-identical RGB 1672×941 PNGs. The explicit centered integer 16:9 export crop is [4, 2, 1668, 938], producing 1664×936, with source-native, 512×288 and 256×144 QA derivatives. Nothing was painted, sharpened, recolored or composited programmatically. Main/code/Git were not edited. This handoff contains source artwork and QA; it is not a live-game or authored-consumer claim.

## Canonical source audit

Canonical identity: C01-STYLE-BORDER-AUTUMN, 灰境边境秋季视觉基准. Existing manifest category is narrative-static and kind is scene, at 256×144. This style topic is a regional visual reference, not reusable map art.

Original source is masters/narrative/scenes/scene-board-03.png, narrative index 23, top-right 2×2 slot (column 1, row 0). The exact inset crop is [632, 5, 1249, 622], 617×617. Full board and exact cell were visually inspected. Source SHA-256 and runtime SHA-256 match the audit; reconstructing the original centered-cover 256×144 export matches current runtime pixels exactly. The old pixels depict an urban parade/civic square, not an autumn rural border. The source discrepancy is preserved and disclosed, not propagated into the replacement or silently treated as a canonical composition.

Story anchors: deep-customized/09-static-assets.md requires fields, bridge, smoke and military flags, with inhabited life before fantasy. deep-novel-v2/01-border-fire-novel.md grounds the early border in overnight rain, grain cloths, villagers saving wheat, practical farms and constrained roads. ART-DIRECTION.md requires cartoon proportions, stable dark contours, restrained regional colors and broad material planes. Accepted ch05-s22 and ch07-s33-est were inspected and used as style-only inputs; their named people, emblems and landmark layouts are not canonical requirements for this plate. No named character identity is asserted.

## Visual review

- Full/native: autumn harvest labor, cottage/granary settlement, a working bridge, hearth smoke, damp road and discreet military presence read in that order
- 512×288: cart, grain-handling figures, bridge opening and fields remain separate, with coherent perspective and no tactical lanes/UI
- 256×144: the rural autumn identity is unmistakable; the two nearest workers, harvest cloths and bridge read as grouped silhouettes. Individual wheat grains and tiny foliage collapse into color texture rather than carrying semantic content
- Broad matte material grouping matches the accepted narrative-plate family. Foliage, grain and small masonry are denser than reusable scatter assets; this is accepted only as an opaque overview/reference, not a standard for single-cell sprite detail
- The blue and red banners with simple ochre edging are new generic illustrative cloth/trim. No source proves them as canonical faction emblems; they must not be promoted into faction marks, identity assets or restored heraldry
- Distant watchtowers and civic cottages are illustrative worldbuilding for the regional setting, not a new map or named location layout

## Modeled-cover QA and limits

All four cover derivatives were visually inspected. They are offline centered CSS-cover geometry simulations only, using current campaign.css image behavior, with no browser, display filters, overlays or text simulation.

- Desktop 960×534: preserves the rural scene, harvest work and bridge
- Staging inset 280×245: trims the far left worker and part of the bridge edge; main cart, autumn cottage and bridge remain readable
- Staging mobile 366×220: preserves most semantic groups
- Portrait story mobile 390×778: materially trims foreground workers, wheat-field breadth and the complete bridge; it retains autumn cottages, part of the cart/bridge and hearth smoke. It is not approved as a complete semantic portrait presentation

There is no proven current consumer for this exact topic: the existing consumer audit is empty, and a fresh exact topic/slug search in packages/story-candidate-01/src and apps returned no matches. Do not add a selector or treat a hypothetical mobile crop as live acceptance without a separate binding/layout decision.

## Retention and acceptance

One generation attempt, no rejected generation attempts. The original mismatched artwork is retained as original-fullboard.png, original-cell-617.png and original-runtime-256x144.png. The exact prompt/request and reference/source hashes are retained. Source-ready for the canonical full 16:9 opaque visual-reference role; no formal binding or runtime replacement was made here.
