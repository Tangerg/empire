# C01 canonical terrain32 source audit

## Result

All **32 canonical identities are uniquely source-mapped by exact regenerated RGBA equality at both 1× and 2×**. This recovers the historical mapping without relying on alphabetical order or the missing `manifest-complete.json`. The four original RGB masterboards decode normally. No source slot was found missing, cross-wired, or composed from unrelated board fragments.

All 32 remain **pending approved-v5 redraw**. They are loaded into the canonical asset catalog with their 2× texture URLs, but **zero current static gameplay ground consumers were found**. The current renderer owns ground through the environment/world-material system. Registering replacement canonical sheets can finish their catalog art contract; it cannot honestly demonstrate changed gameplay ground.

Audit checkpoint: `b179027da36ed4642a76c3b823e50fe3b40b4cfe`. Repository was read-only and clean when checked. A concurrent worker advanced HEAD to `3647f568570cf61ebea98db2dcc0d7316b8433d8` during the audit; final hash comparison confirmed the canonical terrain source/output and consumer inputs stayed unchanged. See `final-drift-check.json`. Shared output files only; no generation, repository edits, commits or full-repository copy. Input hashes make later drift detectable.

## Recovered board order

All boards use a 4×2 grid. These rows are **pixel-proven**, not inferred from filenames:

| Original masterboard | Dimensions | Top row, left→right | Bottom row, left→right |
|---|---:|---|---|
| terrain-atlas-border-river.png |1774×887|BORDER1,2,3,4|RIVER1,2,3,4|
| terrain-atlas-capital-silverwood.png |1448×1086|CAPITAL1,2,3,4|SILVERWOOD1,2,3,4|
| terrain-atlas-forge-wasteland.png |1774×887|FORGE1,2,3,4|WASTELAND1,2,3,4|
| terrain-atlas-graveyard-oathlight.png |1402×1122|GRAVEYARD1,2,3,4|OATHLIGHT1,2,3,4|

Every exact grid crop, four subsequent square sampling windows, source SHA256, runtime file SHA256, decoded-pixel SHA256, dimensions, alpha distribution, and visual identity appears in `canonical-terrain-mapping.json` / `.csv`. `*-proof.jpg` are bounded source-crop contact sheets; `*-runtime-proof.png` show native 1× exports enlarged by nearest-neighbor 2×.

## Identity and derivative accounting

- 32 canonical source identities, not296 identities
- 18 topics have 4 opaque variants; 14 topics have 16 alpha-masked NESW tiles
- 296 logical tile cells per density, 64 PNG runtime/HD outputs total; every variant, mask and density is derived
- Existing 186 registered environment source cells remain separate; the 36 environment atlases and 1188 baseline physical components must not be added to canonical topic progress
- Environment surface sources are separate `environment-builder-v1/masters/surface-common-4x4.png` and `surface-regional-4x4.png`; routes/waters use their own 4×4 masters. Similar material names do not prove the same canonical identity
- One extra old HD environment output, `C01-ENV-TWIN-HILLS-GROUND`, derives from canonical SILVERWOOD1's original source slot using a larger inset, mirror tiling and color adjustments. It is a derivative, adds zero canonical identities, and has no current static consumer found

## Consumer evidence

- `candidate-01-assets.ts:176–226` eagerly resolves canonical assets, excludes1× tactical terrain from runtime glob and selects the HD manifest URL at density2
- `candidate-01-bindings.ts:66–76` explicitly documents removal of `CANDIDATE_01_TERRAIN_ART`
- `candidate-01-runtime.ts:163–187` now returns empty ground markup and draws only map buildings
- `candidate-01-runtime.test.ts:107–127` guards this empty-ground contract
- `candidate-01-terrain-materials.ts` owns actual ground material choices for all campaign levels; `candidate-01-map-scene.ts` composes the environment/world materials
- Repository search of current TS/TSX and scene JSON found no current direct C01-TERRAIN topic binding. Old comments in `picture-textures.ts` naming a terrain PNG are historical examples, not consumers

This is a static consumer audit, not browser/live-game observation. No alternate terrain gameplay path should be restored just to make unused canonical art appear used.

## Export, anchor and edge contract

Flat 32×32 logical cells; no declared anchor, footprint, standing-object foot, height or animation. Variant order is a,b,c,d; horizontal sheets are 128×32 and 256×64. Connected sheets are 512×32 and 1024×64, frames 0..15, N=1/E=2/S=4/W=8. Mask 0 is an isolated central pad, not empty. Mask 15 is a cross. 5/10 are straight NS/EW.

The old exporter samples 4 overlapping square windows at normalized centers (.32,.32),(.68,.32),(.32,.68),(.68,.68), side=round(min(slot size)×.62), LANCZOS-resizes, then applies Color.82 and Contrast.94. Connected sheets discard windows b/c/d and clip window a through generic masks. The 1× and 2× masks are rasterized separately; 2× is **not** defined as resizing the 1× alpha. All exact shapes are in `first-family-border-contract.json`.

Opaque variants are fully opaque. Connected alpha is binary with 11776 transparent / 4608 opaque pixels at 1×,47040 transparent / 18496 opaque pixels at 2× per sheet. No partial-alpha pixels. Connected open edge spans are 8px wide at1× and 16px at2×. All expected alpha pairings match.

**None of the 32 topics is RGB edge-perfect at either density.** This canonical exporter has no color edge-lock operation. `tile-edge-audit.json` records all 64 density/topic results. Do not inherit the environment atlas's `edgeLocked` promise into these canonical sheets. New exports should pass every variant E/W and S/N pair and every connected open-edge pair, plus 2×2 repeated and 4×4 mixed visual fixtures. New material can be made periodic; NESW occupancy and mask geometry stay unchanged.

## Source integrity and semantic traps

The source files are intact; the **historical exporter causes semantic fragments**. This is not evidence of a corrupted PNG or wrong slot mapping.

- Connected topics retain only upper-left window a, then a tiny center/arm mask. Bridgeheads, broken bridge edges, mother-tree roots, molten channels and oath seals lose complete motifs
- Variant topics sample overlapping portions, not four independently drawn versions. Grave rings, name markers, palace edging, nursery boundary and archive rails are cut by sample borders
- GRAVEYARD1's name says “ordinary gravestone ground”, but the observed source is bare ground inside a loose stone ring with no obvious standing gravestones. The observed source, not a speculative cemetery redesign, owns the restyle
- SILVERWOOD3 is a straw/pebble/plant boundary, not a nursery building; WASTELAND2 visibly contains rock ledge faces, not a new terrain-height rule; RIVER3/4 are ground compositions, not additional bridge object identities
- Upper-left directional rut/channel imagery does not magically become an authored turn when a cardinal mask is applied. Do not manufacture new connection rules from a label

`canonical-terrain-mapping.json` includes an observation and specific fragment risk for every topic. Exact mapping does not recover an original prompt or certify that old export design was good.

## First bounded family: BORDER1–4

Choose these four as the first pilot: intact, semantically unambiguous originals on one board and no unique emblem or large structure reconstruction needed.

| Topic | Original crop XYXY (right/bottom exclusive) | Identity | Export |
|---|---|---|---|
|BORDER1|[3,3,441,441]|golden cut wheat/straw on dry earth|4 opaque variants|
|BORDER2|[447,3,884,441]|rainy mud, puddles, ruts|16 NESW masks|
|BORDER3|[890,3,1327,441]|blackened field furrows/stubble, quiet embers|4 opaque variants|
|BORDER4|[1333,3,1771,441]|cart ruts, footprints, pebbles|16 NESW masks|

The pilot authors exactly **four new coherent imagegen materials, one per canonical topic**. Its 40 cells per density (4+16+4+16) are mechanical variants/masks, not 40 artworks. Historical source windows are recorded separately from the new material windows, which must be measured from the actual generated canvas.

Retain the first family's exact alpha topology: no occupancy defect was found. Correct the nonperiodic RGB seams and diagonal-rut clipping through imagegen source design plus documented bounded edge matching and mask/UV composition. Do not paint procedural texture, dirt, ruts, stones or grain in code. The historical .82/.94 color transform explains old pixels; it is not a required recolor of approved new sources. A detailed retain/correct/semantic-gate matrix is included in the contract.

Four exact unedited PNG reference crops are in `first-family-references/`. Source board SHA256 is `a58d135a7de58f4cd473571dfaa75f3ac432a2ec6543499474b156b07617c9dd`.

`first-family-border-contract.json` is the complete worker handoff: source hashes/crops, approved concept reference, native dimensions, alpha masks, style scope, source-derived identity, acceptance fixtures and counting. Ground should remain flat, chunky and matte with restrained microtexture. Existing approved meadow/earth can inform material cohesion only; their source pixels cannot be renamed and counted as these four canonical remakes.

## Finite completion plan

Eight four-topic families, with independent raw/export/catalog stages:

1. BORDER1–4: establish opaque and connected export + edge QA, 4 topics
2. CAPITAL1–4: paving/drain/border material, 4 topics; avoid clipped drainage and palace border
3. FORGE1–4: grates/molten/runes/archive paving, 4 topics; inspect channel fidelity
4. SILVERWOOD1–4: litter/root/boundary/underbrush, 4 topics; roots require deliberate complete local motifs
5. WASTELAND1–4: earth/ledge/camp/weathered material, 4 topics; keep elevation and camp props separate
6. GRAVEYARD1–4: ring/name-grid/contamination/purification, 4 topics; preserve source identity and simplify complete local marker motifs
7. OATHLIGHT1–4: hub/control/disconnection/open network, 4 topics; prevent hub fragments and invented sigils
8. RIVER1–4: bank/shallow/bridgehead/broken edge, 4 topics; highest risk that canonical mask cannot express complete source composition

For each: preserve raw output/prompt/references, explicit per-topic crop/identity registration, native 32px semantic review, two-density export, alpha and seam fixtures, and formal canonical+HD resolver registration/tests. Keep all 32 in the fixed catalog scope. If a family cannot represent its source meaning under the existing mask contract, stop that family's formal acceptance and report the concrete conflict; do not silently substitute an environment asset or alter topology. The next legitimate decision would be an explicit presentation-contract redesign, not a guessed map change.

Completion is32 remade canonical catalog identities with proven source references and exports. Gameplay usage remains separately documented as catalog-only until an intentional authorized consumer binding exists. No live-game completion follows from these files.

## Reproduce and verify

Run the three scripts in this output directory in order: `audit_mapping.py`, `extend_audit.py`, `finalize.py`. They read repository inputs and write only this shared audit directory. The exact-match audit does not import/run the missing-manifest builder main. It replays the small terrain math and compares every candidate at both densities. Result: 32/32 unique source matches, 64/64 decoded-pixel reproductions; all sources and 64 outputs decode; alpha contract valid; 64/64 topic-density sheets fail exact RGB periodic edge equality as expected.

No application build, gameplay test or visual browser run was performed for this read-only source audit.
