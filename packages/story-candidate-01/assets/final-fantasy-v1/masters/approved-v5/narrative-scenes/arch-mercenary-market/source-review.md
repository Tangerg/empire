# Mercenary supply market source review

## Identity and source proof

- Topic: `C01-ARCH-MERCENARY-MARKET`, 佣兵补给市集; narrative-static, scene
- Full opaque landscape setting, not a transparent board structure or reusable character sprite
- Original: `packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/scene-board-11.png`, top-right cell, narrative index 71; exact crop `[632, 5, 1249, 622]`
- The full original board and exact 617×617 cell were viewed and the source hash verified. The copied full board is byte-identical. Original 256×144 runtime pixels independently reproduce from that cell
- Both this original topic and its consumers were audited. It has no current shipped consumer. Shared story/staging CSS geometry is tested as future-use crop stress, not claimed live-game use

The original full board11 top-right cell shows a pine-and-stone enclosed market with canvas stalls, wagons and a draft animal, numerous mercenaries, a central ledger table, practical goods and a rear display board bearing round disk-like wares.

## Semantic reconstruction

- Expanded the square original into a clear landscape with fewer anonymous role figures and grouped goods
- Reinterpreted original round display marks as undecorated physical shields hung from pegs, avoiding invented heraldry
- Paired ordinary boots on the trade table support practical provisioning and the prose emphasis on soldier accounting without declaring a specific death or named identity
- Reduced ground noise to broad ochre planes, sparse large paving stones and quiet shadows

## Lore sources

- `docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md:84`: C01-ARCH-MERCENARY-MARKET: 佣兵补给市集
- `docs/story-candidates/candidate-01/deep-customized/02-grey-banner-exile.md:52-70`: Mercenaries negotiate concrete sums, dates and recipients; paired boots are returned to families and single boots serve as identity evidence. The first pension register is established. This context informs practical accounting rather than depicting Tasha or a specific bereavement.

## Selected source and dimensions

- Built-in image_gen; one attempt; exact saved prompt and request in this bundle
- References: exact original cell for content; approved Gray Banner camp and field hospital masters for style
- `raw.png`, `native.png`, `master.png` are byte-identical unchanged tool output: RGB 1672×941, fully opaque
- SHA-256: `fe658317d258668b4d3ae9ea6a7f2b5aa2ac1d6096f23754eaaba00d6520ff4c`
- Proposed explicit source-native crop `[4,2,1668,938]` produces 1664×936, matching 256×144 at density 6.5
- Only QA derivatives are cropped/resized; full authored master preserved. No programmatic artwork edits

## Visual acceptance

At native size the trade table, paired boots, ledger, surrounding goods and stalls read distinctly. Canonical 256x144 still reads as a supply market with a central transaction. Desktop cover retains the loaded wagon and layout. Narrow 390x778 centered cover keeps the quartermaster, large open ledger, paired boots, both customers and plain shield merchandise behind. Broad ground remains quiet; no emblem, named face or invented faction mark is present.

## Limits

- No current shipped consumer or branch/age selector exists for this catalog topic
- Not a portrait of Tasha and not a depiction of her named introduction, pension payment or specific contract signing
- The draft animal reads horse-like; its functional wagon/pack-animal identity is preserved, with no lore requiring a specific breed
- Narrow center cover loses the outer wagon and most right stalls, while retaining ledger, boots, quartermaster, customers, supply stall edge and shield display

No repository code, exporter, selector, binding, live UI or Git state was modified by this worker. Native-source acceptance does not claim formal or live completion.
