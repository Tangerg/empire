# Gray Banner field camp source review

## Canonical identity

- Topic: `C01-ARCH-GRAY-CAMP`, 灰旗野战营; category `narrative-static`, kind `scene`
- This is an intentionally opaque complete setting illustration. It is not a transparent reusable building, board sprite, architecture cutout, or gameplay map
- Original source: `packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/scene-board-01.png`; 1254×1254; top-right cell, original narrative index 7; exact inset crop `[632,5,1249,622]`
- Source authority: `docs/art-rebuild-20261019/approved-v5/remaining-narrative-audit/manifest.json`, with catalog-order mapping and original runtime-pixel verification. The full board and 617×617 original cell were inspected, not just the topic label

## Source and story interpretation

The original cell contains gray canvas tents, pale radial marks on gray banners, stores and supply carts, a repair/forge area, a medical side tent, anonymous compact soldiers and paths through a wooded rocky camp. `deep-customized/09-static-assets.md:16` independently calls for tents, muddy ditches, supplies and command areas. Art direction requires silver-gray and warm amber, broad material planes and strong contours.

The historical remaining-narrative audit records no actual consumers. The later council-selector audit justifies using this existing topic for `c01/aftermath-10`: Gray Banner members in the camp outside the mountain pass hear their right to leave again. The new plate therefore adds a quiet practical ledger discussion to a generic working Gray Banner camp, with a distant pass. It does not claim to depict the exact pledge, named speakers or pass geography. It must not be used to identify the earlier hostile Black Camp.

## Flag and ward-marking caveat

Both the pale radial-star marks on gray flags and the red cross on a pale patch on the camp's medical tent are visibly present in the original top-right source cell and full board. The remake retains those source-derived visual cues. They are not newly invented faction lore. The star shape is reinterpreted rather than pixel-exact heraldry.

The campaign prose describes the FIRST gray flag as smoke-stained cloth with an incompletely removed blue line (`deep-customized/01-border-fire.md:123–157`). Later dragon-wing personalization is introduced in chapter four (`04-grey-banner-queen.md:9`). Other approved early scenes deliberately use plain gray flags. No consulted prose establishes the radial star as the universal Gray Banner emblem. This generic camp source therefore preserves its own legacy mark without asserting that every Gray Banner scene or era should use it; it is not a replacement for the first-flag prop. No crown or dragon-wing emblem was added. No actual contradiction requiring a redesign was found.

## Selected generation and style

- Built-in image_gen, two attempts; selected attempt 2
- Attempt 1 preserved under `rejected-v1/`: narrative semantics accepted, but dense ground mottling and tiny puddle fragments failed the approved quiet-surface treatment
- Attempt 2 was a targeted ground-only correction. The selected image has broad matte ochre earth, continuous simple mud/puddle shapes, firm dark contours, chunky tents, compact anonymous figures and a clearly readable central ledger table
- Approved concept-v2, current public-rations and corrected ch02-s06 illustrations were used as style references; the original camp cell provided content identity
- All people are anonymous camp roles. No named hero or Laiya likeness was requested or represented. The records contain non-readable line marks only

## Native dimensions and export recommendation

- `raw.png`, `native.png` and `master.png` are byte-identical copies of the selected tool output, RGB 1672×941, fully opaque
- SHA-256: `cf742aad26c2cb50edc82f16667aae3a465d050a2d5a3ce6216b94f8e04487f5`
- Proposed centered source-native crop: `[4,2,1668,938]`, giving 1664×936. Remove only 4 px left/right, 2 px top and 3 px bottom; no enlargement or fabricated detail
- Canonical logical scene remains 256×144. A registered physical 1664×936 export has density 6.5. QA derivatives are explicitly separate from the untouched raw/master

## Visual verification

Full native image and offline CSS-cover views were inspected. Desktop retains the complete working camp with supply, forge, ward and pass context. The narrow centered mobile crop retains the command tent, large gray flag, open ledger and three ordinary camp members. Both staging-shaped secondary checks also retain the focus. `qa-covers-contact-sheet.png`, `viewport-qa.json` and `source-qa.json` record these tests.

This worker did not modify repository code, register an HD export, change selectors, perform a live browser check or mark formal binding complete. Offline crop checks are not live-game evidence.
