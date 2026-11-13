# C09 hand-painted scene

Existing level `c01-09` only. No level, movement, collision, AI, combat, objective or campaign definition changes. The authored scene uses the existing assembled-ground renderer and projection.

## Source and ownership

- Ground, low field-post props and weathered horizontal wall were created with the built-in image generation tool. C01 hand-painted ground informed the style; the wall preserves the C08 wall footprint and anchor. Only selected final assets are included.
- Houses, pine sprites and short wall reuse their existing registered sources. The catalog records their original paths.
- Seven terrain sites have one facade owner each; four dynamic command nodes retain their original lifecycle. The two `B` cells remain temporary field posts, not bridges. Capturing a field post does not itself neutralize its node.
- Five hills and the single mountain are painted into continuous ground, not separate colliding or selectable objects. Terrain positions and rules remain owned by the canonical level. Painted slopes may extend beyond the cell center; the eastern hill crest sits east of the registered terrain contact.

## Validation and limits

The `e112a814` base plus the earlier frozen C09 assembly passed the full native gate, including all 34 pre-existing native scenes and 16 independent replay/save battles. The final continuous-ground difference passed assembled-ground, scene-surface, camera-integration, tactical-HUD and battle-screen checks, plus C09 source/render/picking checks. The release tree separately passed formatting, metadata, build, import, C09 checks and gate-list self-tests after adding C09 to the aggregate. This is not a claim that the final tree ran one uninterrupted aggregate invocation.

Production C08 replay and settlement were validated for both contracts with and without the ledger; each entered the C09 branches with distinct flags and relationship increments. Ledger-carrying C09 engineer and ritual routes reached actual victory with legal commands, exact replay and save admission. The preserved earlier history is C07/C08, not an invented C01–06 campaign.

Actual native GUI checks covered the formal entry, narrow layout, cast/save/undo/reload, carried engineer node 2-to-3 victory and settlement, target-location frames on occupied nodes, and a live node's exposed-edge picking and 35/100 dossier. Visual checks are separate from headless tests. No export-package acceptance or complete 16-level victory claim is made.

Private saves, screenshots, rejected art, exploratory exports and credential material are intentionally not included.
