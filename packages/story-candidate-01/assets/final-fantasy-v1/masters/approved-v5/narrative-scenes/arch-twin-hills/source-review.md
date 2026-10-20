# Twin Hills battlefield location source review

## Canonical identity and evidence

- Topic: `C01-ARCH-TWIN-HILLS`, 双子丘陵战斗地点; category `narrative-static`, kind `scene`
- Original board: `packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/scene-board-09.png`; RGB1254×1254, top-right cell, original narrative index 63; exact historical inset crop `[632,5,1249,622]`
- Original board SHA256: `bea6f843b636aba30bfac221569c8fb72a3c21747598a47a9920c81adfbca840`; matches the recovered audit
- Full original board and extracted617×617 cell were both visually inspected before prompting. The whole generated composition is intentionally opaque; it is not a transparent building, board structure or battle map

## Source and lore interpretation

The inspected original scene-board-09 top-right cell is an empty elevated battlefield landscape: a narrow stream separates opposing terraced rock masses, with two timber bridges, multiple empty fenced clearings, winding dirt paths, short wooden stairs, pines and sparse plants. It contains no army deployment, named hero, village or memorial. The remake preserves that geographic logic while consolidating individual small clearings into a readable wide landscape; it does not claim to be a coordinate-faithful map.

The catalog names this topic 双子丘陵战斗地点. deep-customized/01-border-fire.md describes the central village drying wheat and Torren advancing to the north hill, while deep-customized/09-static-assets.md requests year-zero wheat and year-four memorial variants. Those story details do not license inventing positions in this source. Neither variant has a current selector. They were not added. The shipped opening stories use C01-CH01-S01 instead.

The read-only current source scan found no direct TS/TSX/JS/CSS consumer for this exact topic. The historical audit also records catalog-only usage. No selector was added. Consumer recheck and source hashes are saved separately.

## Generation and style

Built-in image_gen, two attempts. `request.json` and `prompt.txt` are the complete first-generation request; `request-v2.json` and `prompt-v2.txt` are the exact selected surface-simplification edit. Both reference sets are hashed. Attempt1 and its exact request/provenance remain under `rejected-v1/`.

The first candidate had correct paired terrain and both crossings, but excessive small ground fragments and dense green speckling. Attempt 2 retains that established geography and turns the paths into broad ochre bands, the grass into quiet connected planes, and the rocks into large slate facets. This matches the approved chunky matte narrative treatment more closely.

## Native preservation and proposed export

`raw.png`, `native.png` and `master.png` are byte-identical copies of the selected tool output, RGB and fully opaque. SHA256: `64aa4e54efd0ce8f31413aa5f82972796bb7a7c006830e754c6d1b38c490bbe0`.

The tool returned 1671×941, one pixel narrower than the requested target. No padding, interpolation or resizing was applied. The explicit common-family crop [4,2,1668,938] removes 4 px left, 3 px right, 2 px top and 3 px bottom. It yields exact source-native1664×936 and density6.5 for logical256×144. QA-only crops/resizes are separate from immutable native sources. No artificial upscaling or programmatic artwork repainting occurred.

## Visual inspection

Both facing high-ground masses, all six empty fenced clearings, all path connections and both bridge crossings are readable at full size and 256×144. No people, tactical labels or direction assignments appear.

Centered 390×778 portrait cover retains the stream, waterfall and both crossings, but removes almost all six fenced clearings and the full paired upper hill shoulders. This is insufficient to communicate the complete Twin Hills geography. Preserve a full landscape view or design a separately authorized portrait composition if a future consumer needs both hills. The 280×245 inset also trims outer clearings; 366×220 is near-complete. No current consumer was changed.

The full native master,256×144 logical downsample,960×534 landscape cover,390×778 centered portrait cover,280×245 inset and366×220 cover were inspected. These are offline hypothetical compatibility checks using the existing narrative CSS geometry, because this topic currently has no live consumer. They do not simulate text/filters/overlays, and are not browser or live-game evidence.

## Handoff limits

The source bundle is ready for parent review and explicit opaque-scene registration. This worker made no repository edits, Git changes, production changes, formal registration, selector changes or live-game acceptance claim. Scope is only the assigned canonical ID. References and proofs are in this folder.
