# C01-STYLE-CAPITAL-ORDER source review

## Outcome and scope

One built-in image_gen call produced an opaque regional visual-reference plate. Selected raw, native and master are byte-identical RGB 1672×941 PNGs. The explicit centered integer 16:9 export crop is [4, 2, 1668, 938], producing 1664×936, with source-native, 512×288 and 256×144 QA derivatives. Nothing was painted, sharpened, recolored or composited programmatically. Main/code/Git were not edited. This handoff contains source artwork and QA; it is not a live-game or authored-consumer claim.

## Canonical source audit

Canonical identity: C01-STYLE-CAPITAL-ORDER, 阿斯塔里亚王都秩序. Existing manifest category is narrative-static and kind is scene, at 256×144. This style topic is a regional civic visual reference, not reusable architecture or a tactical map.

Original source is masters/narrative/scenes/scene-board-04.png, narrative index 27, top-right 2×2 slot (column 1, row 0). The exact inset crop is [632, 5, 1249, 622], 617×617. Full board and exact cell were visually inspected. Source SHA-256 and runtime SHA-256 match the audit; reconstructing the original centered-cover 256×144 export matches current runtime pixels exactly. The old pixels depict a wooded tent camp in rain, not the capital. The discrepancy is preserved and disclosed, not propagated into the replacement or silently treated as a canonical composition.

Story anchors: deep-customized/09-static-assets.md requires clean, safe, genuinely attractive order alongside the unease of synchronized kneeling. deep-novel-v2/07-throne-without-king-novel.md describes real bread, clean water, working hospitals, clean streets and citizens kneeling together, with mixed fear and sincere relief. The narrative bible prohibits making the crown an obviously evil object or assuming all ordinary people support the alternative. The continuity appendix requires an operating city rather than a ruin filter. Accepted ch05-s22 and ch07-s33-est were inspected and used as style/civic vocabulary references. This is not a literal Ansel scene, not an exact city skyline, and not a new claim about the seven towers. No named face is asserted; character reference identities are not consumed.

## Visual review

- Full/native: usable bread counter and supply sacks, a flowing public fountain and staffed hospital beds make the real public benefit readable. The dignified pale-stone city remains intact and safe
- Four different adult townspeople kneel in one aligned foreground group, knees down and hands lowered. Their synchronized bowed posture creates the discomfort without a villain ruler, black armor, spikes or red sky
- 512×288: the four silhouettes remain countable, fountain separates from people, and bread/medical functions remain visible
- 256×144: the kneeling group and three services remain distinguishable through broad shapes and value grouping; small bottles and leaf texture are nonessential
- Broad outlined cel-shaded figures, squat masonry and matte cloth fit the accepted narrative scene family. Minor bright water highlights are localized; foliage and masonry detail are denser than reusable sprite assets, acceptable for an opaque overview
- Blue banners with simple ochre edging are generic illustrative trim, not restored or newly established canonical faction emblems. No exact faction identity may be inferred from them
- The visual vocabulary deliberately resembles the accepted civic-service reference, but named protagonists and Ansel's distinctive service-tower layout are absent. The capital reference is a new illustrative street, not an architectural floor plan or authored game location

## Modeled-cover QA and limits

All four cover derivatives were visually inspected. They are offline centered CSS-cover geometry simulations only, using current campaign.css image behavior, with no browser, display filters, overlays or text simulation.

- Desktop 960×534: preserves all three service groups and all four kneeling people
- Staging inset 280×245: loses much of the bread counter and clips the fourth person/hospital edge; it retains the synchronized group and fountain but is incomplete for the full civic-benefit argument
- Staging mobile 366×220: preserves most of the full composition and service groups
- Portrait story mobile 390×778: cuts off the bread counter, medical arcade and outer people. It retains the fountain and central kneeling figure with partial neighbors, but loses the complete civic-services contrast. It is not approved as a full semantic portrait presentation

There is no proven current consumer for this exact topic: the existing consumer audit is empty, and a fresh exact topic/slug search in packages/story-candidate-01/src and apps returned no matches. Do not add a selector or claim the modeled mobile/staging crop is live acceptance without a separate binding/layout decision.

## Retention and acceptance

One generation attempt, no rejected generation attempts. The original mismatched artwork is retained as original-fullboard.png, original-cell-617.png and original-runtime-256x144.png. The exact prompt/request and reference/source hashes are retained. Source-ready for the canonical full 16:9 opaque visual-reference role; no formal binding or runtime replacement was made here.
