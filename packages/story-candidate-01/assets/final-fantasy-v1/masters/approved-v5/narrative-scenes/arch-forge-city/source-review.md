# Mountain Forge city architectural setting source review

## Canonical identity and evidence

- Topic: `C01-ARCH-FORGE-CITY`, 山炉炉城; category `narrative-static`, kind `scene`
- Original board: `packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/scene-board-10.png`; RGB1254×1254, top-right cell, original narrative index 67; exact historical inset crop `[632,5,1249,622]`
- Original board SHA256: `75c85d844f0d2c0fe46974218875ee07f94ace55b97b80e6e9259d32cb5501ee`; matches the recovered audit
- Full original board and extracted617×617 cell were both visually inspected before prompting. The whole generated composition is intentionally opaque; it is not a transparent building, board structure or battle map

## Source and lore interpretation

The inspected original scene-board-10 top-right cell is an exterior industrial settlement built into exposed rocky terraces, with separate furnace houses, bronze-banded pipes and wheels, wood hoists, stairs, elevated walkways, small craft workers and a timber bridge over a lower stream. Its architectural ensemble is the subject. It is visually distinct from scene-board-07 top-right C01-CH03-S14 and the already approved underground joint-repair master, which were inspected for distinction.

The catalog calls this topic 山炉炉城. deep-customized/03-elder-peoples.md describes shift bells, metal contract plates, work crews and ordinary miners; its joint-repair narrative is explicitly C01-CH03-S14. deep-customized/09-static-assets.md asks this architectural topic to support furnaces, archives and repairable structures. The remake keeps the actual architectural source identity and workday context, without inventing a labeled archival location, faction event or named character. The shipped forge stories remain bound to C01-CH03-S14.

The read-only current source scan found no direct TS/TSX/JS/CSS consumer for this exact topic. The historical audit also records catalog-only usage. No selector was added. Consumer recheck and source hashes are saved separately.

## Generation and style

Built-in image_gen, two attempts. `request.json` and `prompt.txt` are the complete first-generation request; `request-v2.json` and `prompt-v2.txt` are the exact selected surface-simplification edit. Both reference sets are hashed. Attempt1 and its exact request/provenance remain under `rejected-v1/`.

The first candidate preserved the city identity but had dense small cobblestone paving, cliff flecks and repetitive seam/rivet detail. Attempt 2 substitutes broad calm stone slabs, large slate cliff facets and grouped foliage while keeping major buildings, wheels, hoists, stairs, workers and fire locations intact. Furnace glow remains local; the surrounding cliffs are legible in daylight.

## Native preservation and proposed export

`raw.png`, `native.png` and `master.png` are byte-identical copies of the selected tool output, RGB and fully opaque. SHA256: `562489fa6fc91adf1ea4dbe66abe89f909309d54245aed7b250eec917fe8b21e`.

The selected output is native1672×941. The explicit common-family crop [4,2,1668,938] removes 4 px left/right, 2 px top and 3 px bottom. It yields exact source-native1664×936 and density6.5 for logical256×144. QA-only crops/resizes are separate from immutable native sources. No artificial upscaling or programmatic artwork repainting occurred.

## Visual inspection

The complete city ensemble, central furnace house, upper forge, right forge, left machinery, stair connections and lowest bridge remain readable in full and 256×144 views. Anonymous role figures stay secondary. No central ledger table, emergency repair ritual, named hero or underground cavern enclosure appears.

Centered 390×778 portrait cover preserves the complete central furnace house, some upper hoist context, descending paths/terrace edges and the lower bridge. It clips the outer workshops, most of the large upper forge, left cooling works, tree border and distant mountain horizon. The whole multi-building city reads much more fully in landscape. The 280×245 inset keeps the central/upper/lower tier logic but reduces side context. No current consumer was changed.

The full native master,256×144 logical downsample,960×534 landscape cover,390×778 centered portrait cover,280×245 inset and366×220 cover were inspected. These are offline hypothetical compatibility checks using the existing narrative CSS geometry, because this topic currently has no live consumer. They do not simulate text/filters/overlays, and are not browser or live-game evidence.

## Handoff limits

The source bundle is ready for parent review and explicit opaque-scene registration. This worker made no repository edits, Git changes, production changes, formal registration, selector changes or live-game acceptance claim. Scope is only the assigned canonical ID. References and proofs are in this folder.
