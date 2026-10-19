# Read-only cliff source audit and bounded export contract

## First redraw batch

All four sources belong to **cliff-modules-temperate**, a fixed 4×4 independent RGBA module sheet, **128×128 logical / 256×256 HD** per cell. The existing atlas foot is **(64,127)**, corresponding to (128,254) at HD density. It is a runtime placement contract, not an annotated point in the original 1254² source.

| Full semantic ID | Index / HD crop | C01-01 / C01-02 instances | Actual scale range |
| --- | --- | --- | --- |
| temperate-hill-cap | 4 / (0,256)–(256,512) | 9 / 4 | 0.471–1.000 |
| temperate-cliff-convex-corner | 8 / (0,512)–(256,768) | 9 / 0 | 0.567–1.000 |
| temperate-pillar-wide | 12 / (0,768)–(256,1024) | 9 / 0 | 0.578–0.674 |
| temperate-plateau-cap | 15 / (768,768)–(1024,1024) | 9 / 0 | 0.566–0.676 |

These four account for **40/64 instances** and **64.85% of alpha-weighted source area after placement scaling, before scene occlusion** across the two actual full scenes. These counts prioritize work; they are not extra completed topics or final screen-visible coverage. Remaining cliff modules are low-ledge (10), talus-foot (7), mountain-pass (3), straight-south (2) and ramp-level-2 (2).

The two first-chapter authored hill caps and convex corners use scale 1.0, so test both that large placement and the smaller procedural placements before accepting new art. Keep continuous aggregated top planes, coherent cliff faces and a few readable rock masses. Do not paint each fragment of the old broken runtime crop as a separate asset.

## Exact inputs

All paths below are relative to `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/`:

- Full keyed RGB master: `masters/cliffs-temperate-4x4-key.png`, 1254×1254
- Full RGBA intermediate: `intermediate/alpha/cliffs-temperate-4x4.png`, 1254×1254
- Logical runtime: `runtime/atlas/cliff-modules-temperate.png`, 512×512
- HD runtime: `runtime/atlas/cliff-modules-temperate@2x.png`, 1024×1024
- Definitions: `manifest-environment-builder-v1.json` and `SEMANTIC-CATALOG.json`
- Historical construction: `tools/build_environment_builder_v1.py`, `module_atlases`, `grid_cell`, `fit_sprite`

The complete original master and alpha image were visually inspected. Source SHA-256 values and full per-instance inventories are in `cliff-source-contract.json`; these cliff sources matched both the isolated worktree and integration at inspection. The complete-source references are named `<semantic-id>-complete-subject-reference.png`; keyed references and original runtime-slot crops are alongside them. `priority-source-contact.png` puts complete subjects above the actual legacy slots.

### Why old slots are unsafe redraw references

The original master is not an integer-cell 4×4 sheet: quarters are 313.5 pixels wide/high. The old builder rounds quarter boundaries, crops each slot, then crops its alpha bounding box and fits the result into 256² with 12-pixel padding. This treats neighbouring fragments as part of a subject.

- The complete **pillar-wide** subject begins at original y=934, while slot 12 begins at y=940. Its top six rows are assigned to slot 8 instead, yielding the visible detached tip below the convex-corner runtime drawing and a clipped pillar top
- The complete **plateau-cap** begins at x=939, while slot 15 begins at x=940, clipping its leftmost column
- Slot 8's alpha bounding box includes the foreign pillar tip. Its measured legacy full-alpha height therefore overstates the actual convex-corner body height
- Reference crops include the whole dominant original subject plus 10 pixels of surrounding transparent/keyed canvas. They preserve source pixels and are diagnostic references, not approved registration coordinates or replacements

New masters should contain complete independent RGBA subjects with reviewed `sourceFoot`, full-canvas registration and no inherited neighbouring fragments. Do not reconstruct master feet from cropped alpha bounds. Register against immutable runtime baselines so unselected cells remain exactly as shipped, including their old defects until separately replaced.

## Actual consumers and mask distinction

`candidate-01-terrain-materials.ts` selects hill-cap/low-ledge/talus-foot for hill cells at 0.46 plus deterministic 0…0.12 scale variation, and pillar-wide/plateau-cap/mountain-pass/convex-corner for mountain cells at 0.56 plus 0…0.12. `sceneryPieces()` in `candidate-01-map-scene.ts` chooses the semantic module, horizontal jitter/mirror and world foot; its normal BoardPiece contact is (0,0). There is no cliff-specific mask selection in that consumer.

`c01-01.scene.json` additionally authors eight larger modules: two each of hill-cap, straight-south, convex-corner and ramp-level-2. C01-02 has no such authored cliff cluster; all seven observed modules there are hill scenery. The `elevationBindings` document field is not consumed by the production scene renderer.

Actual complete captures contain **57 temperate cliff modules in c01-01** (19 actors, 4 rule buildings) and **7 in c01-02** (20 actors, 9 rule buildings). Both contain **zero elevation-auto-* atlas instances and zero cliff-modules-highland instances**. Scene snapshots are enumerated and hashed in the JSON. Context crops are nearest-neighbor enlargements from those complete images, not recomposed selected objects.

The historical builder does also derive separate `elevation-auto-temperate` / `elevation-auto-highland` sheets from the first cliff source. Those are 16 NESW masks, 32×48 logical / 64×96 HD, bits N/E/S/W, drawn by `elevation_edge_cell()` with fixed polygons and caps. They share a historical source, but not the active renderer path or the new module export. Do not regenerate them or modify their topology as part of this batch. Existing GameMap terrain/elevation, movement, passability, ramp rules, triggers and actor placement remain authoritative and untouched.

## Adapter support included with this audit

The existing `export_environment_overrides.py` now narrowly recognizes the above 4×4 / 128 / 256 / (64,127) module contract. Its legacy `cellOrder` contains short labels while `cells[].id` contains full semantic IDs, so a fixed 16-pair validation preserves the exact mapping without rewriting either array. Registration keys and export reports use the full IDs. Other atlases retain their existing matching-label contract.

The existing review reader now locates `cells[].id`, as the runtime catalog does, instead of assuming a semantic ID is also a short `cellOrder` label. It still reads the actual atlas dimensions/columns. No new parallel exporter, runtime renderer, mask, catalog registration, source art or derived runtime PNG was added.

Tests cover the four non-first-row target indices, exact dimensions/anchors, source alpha including 1/255 residue, every unselected cell's pixels, unchanged semantic/ramp/elevation metadata, automatic-mask file bytes, mixed atlas sizes, rejection of mismatched/swapped label-ID mappings and native review measurements at 128 pixels. The deliberately disabled pairing guard and shifted output slot are both rejected by regressions. Verification results are in `verification.json`.

Before first real export, preserve/checksum the old 1x/2x runtime sheets as immutable masters and register only approved complete sources. The cliff atlas remains unregistered in `environment-overrides.json` in this commit. No production pixels change until that separate integration step.
