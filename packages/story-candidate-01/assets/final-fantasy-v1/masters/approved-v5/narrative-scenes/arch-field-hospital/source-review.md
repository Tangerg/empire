# Holy Light field hospital source review

## Canonical identity

- Topic: `C01-ARCH-FIELD-HOSPITAL`, 圣辉野战医院; category `narrative-static`, kind `scene`
- This is an intentionally opaque complete hospital setting illustration. It is not a transparent reusable hospital building, board sprite, or gameplay map
- Original source: `packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/scene-board-11.png`; 1254×1254; top-left cell, original narrative index 70; exact inset crop `[5,5,622,622]`
- Source authority: `docs/art-rebuild-20261019/approved-v5/remaining-narrative-audit/manifest.json`, with catalog-order mapping and original runtime-pixel verification. The 617×617 source cell was visually inspected before generation; a full-board copy is retained

## Source and story interpretation

The original cell contains a large pale ward tent with gray valances and modest gold forked-staff/cross-like motifs, orderly cots, cream/gray hooded caregivers, warm lamps, a stone washing basin, bottles and bandages, a medicine/record desk, supply containers and a peripheral fenced burial area. These details, rather than the topic name alone, define this remake.

The historical remaining-narrative audit records no actual consumers. The later council-selector audit justifies reuse for `c01/aftermath-13`, whose setting is the old hospital outside the holy city and whose action is examining recovered records while retaining names and alteration history. `deep-customized/03-elder-peoples.md:45–69` explains that the Holy Light institution provides genuinely valuable hospital care even while its archive practices are questioned.

The new illustration keeps hospital care dominant: a pale open ward, occupied cots, caregivers and washing/supply work. A modest central record table with two anonymous readers supports the aftermath reuse. A weathered low stone wall and distant city provide exterior-location context. They are interpretation of the supported setting, not a claim to reproduce an established hospital floor plan. No Holy Archives interior or White Tower archival architecture was invented, and this asset must not substitute for the archive-raid briefing.

The original gold religious motif is retained as a simplified visual family on gray fabric, not asserted as a pixel-exact heraldic specification. Plain graves remain peripheral to the active care area. Every figure is an anonymous caregiver or patient; the record readers are not claimed to be Mirelle, Laiya or other named characters.

## Selected generation and style

- Built-in image_gen, one attempt; no rejected or corrective generation required
- Approved concept-v2 and public-rations illustrations provide the chunky narrative style. The selected Gray Banner camp is an additional reference only for broad quiet ground treatment, not for hospital faction identity
- Strong stable dark contours, squat pale tent architecture, broad matte cream/gray folds, clean warm ochre ground with a few larger slabs, restrained gold/amber, and compact figures
- No micro-speckled ground or photorealistic texture. Records contain only non-readable line marks; no invented readable text
- Pale ward, caregiver/patient and small record-reading group remain clearly separated and legible

## Native dimensions and export recommendation

- `raw.png`, `native.png` and `master.png` are byte-identical copies of the selected tool output, RGB 1672×941, fully opaque
- SHA-256: `8a78169f8661da0811601692b02e16aeb90c5e63b4fe4d7f7a1ecec5fe24d37e`
- Proposed centered source-native crop: `[4,2,1668,938]`, giving 1664×936. Remove only 4 px left/right, 2 px top and 3 px bottom; no enlargement or fabricated detail
- Canonical logical scene remains 256×144. A registered physical 1664×936 export has density 6.5. QA derivatives are separate from the untouched raw/master

## Visual verification

Full native image and offline CSS-cover views were inspected. Desktop retains the complete ward, washing basin, supply cart, care activity, record desk and peripheral cemetery/city context. The narrow centered mobile crop retains the pale ward entrance and gold motif, an attended patient, and both record readers with their table. It therefore preserves hospital identity as well as the supported aftermath activity. Both staging-shaped secondary checks also retain this focus. `qa-covers-contact-sheet.png`, `viewport-qa.json` and `source-qa.json` record these tests.

This worker did not modify repository code, register an HD export, change selectors, perform a live browser check or mark formal binding complete. Offline crop checks are not live-game evidence.
