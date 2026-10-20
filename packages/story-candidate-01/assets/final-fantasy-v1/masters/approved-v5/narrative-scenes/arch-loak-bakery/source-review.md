# Loak bakery source review

## Identity and source proof

- Topic: `C01-ARCH-LOAK-BAKERY`, 洛岬玛尔塔面包房; narrative-static, scene
- Full opaque landscape setting, not a transparent board structure or reusable character sprite
- Original: `packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/scene-board-10.png`, top-left cell, narrative index 66; exact crop `[5, 5, 622, 622]`
- The full original board and exact 617×617 cell were viewed and the source hash verified. The copied full board is byte-identical. Original 256×144 runtime pixels independently reproduce from that cell
- Both this original topic and its consumers were audited. It has no current shipped consumer. Shared story/staging CSS geometry is tested as future-use crop stress, not claimed live-game use

The original full board10 top-left cell shows a timber-and-stone gabled bakery, central arched bread oven, separate round stone bake-oven, two striped awnings, bread carts, small civilian and helmeted queues, river/landing, pines and stone fences.

## Semantic reconstruction

- Expanded the square source into a deliberately framed landscape rather than cropping it to the old square scene
- Reduced crowd density and small ground detail for broad matte-plane readability
- Added a modest open account book and grouped bread sizes to support the documented public-accounting arrangement
- Named bakery retained as a setting, with anonymous baker role figures rather than an unverified canonical portrait of Marta

## Lore sources

- `docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md:79`: C01-ARCH-LOAK-BAKERY: 洛岬玛尔塔面包房
- `docs/story-candidates/candidate-01/deep-customized/02-grey-banner-exile.md:32-50`: Marta adapts an abandoned bathhouse for baking; military bread, relief bread and paid bread have public accounts. Different governance choices change supply and access.
- `docs/story-candidates/candidate-01/deep-customized/00-creative-bible.md:60-64`: Recurring bakery visits show governance through bread size, price and ingredients; the baker records practical effects of each flag.
- `docs/story-candidates/candidate-01/deep-customized/09-static-assets.md:97`: Planned asset includes refugee hard bread, prosperous gray-wing bread and scarcity price boards, but current topic is one image without stage selector.

## Selected source and dimensions

- Built-in image_gen; one attempt; exact saved prompt and request in this bundle
- References: exact original cell for content; approved Gray Banner camp and field hospital masters for style
- `raw.png`, `native.png`, `master.png` are byte-identical unchanged tool output: RGB 1672×941, fully opaque
- SHA-256: `344ced5f9e3a27948f94c9e8f3a09edad6c6eeefe5a439d95bd2d984c10248d2`
- Proposed explicit source-native crop `[4,2,1668,938]` produces 1664×936, matching 256×144 at density 6.5
- Only QA derivatives are cropped/resized; full authored master preserved. No programmatic artwork edits

## Visual acceptance

At native size the ovens, large bread shapes and service exchange read cleanly. The 256x144 canonical derivative retains the gabled bakery, both ovens, bread carts and mixed queues. Desktop cover preserves the full location. Narrow 390x778 centered cover preserves the central bread exchange, account book, bread display, awning and part of the glowing oven. Ground remains broad ochre with sparse large paving stones. No invented symbol or recognizable named hero appears.

## Limits

- Old bathhouse adaptation appears only as a reused stone ground floor; the legacy source itself depicts a gabled bakery, so its visual identity was preserved instead of inventing a new bathhouse design
- No branch-specific price, later gray-wing pastry emblem, timestamp or governance outcome is depicted
- The narrow center cover clips most of the left oven arch and loses the separate round oven and queues; it retains glowing bread, the baking frontage, loaf counter, ledger and bread exchange

No repository code, exporter, selector, binding, live UI or Git state was modified by this worker. Native-source acceptance does not claim formal or live completion.
