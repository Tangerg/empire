# Registered transparent master exports

## Approved broad field material

`packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-field-material-v2.json`
owns the approved field source/checksum, the exact replaced atlas/tone, logical
sampling period, world origin, uncropped fit and both runtime paths. The typed
`candidate-01-field-material.ts` reader and `export_field_material.py` consume
that same declaration. This binding replaces only `surface-meadow` tone 0;
the old atlas remains intact and hill tone 2 retains its original fit.

```sh
python3 tools/art/export_field_material.py
python3 tools/art/export_field_material.py --check
node_modules/.bin/vite-node tools/art/review_ground_runtime.ts
python3 tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/ground-runtime-1024 --compare-directory docs/art-rebuild-20261019/approved-v5/ground-runtime
```

The immutable approved grass master is resized as a complete canvas to 512²
and 1024². Only the opposite outermost rows/columns are matched, using the
previously approved 1px seam operation. Interior pixels, palette and composition
are not painted, blurred or recolored. Reports include source, interior and
edge hashes; `--check` verifies reproducible output without writing.

Runtime now samples a **1024×1024 logical period** from world origin (0,0),
with explicit inset 0 and bleed 0. Schema 2 separates `rasterSize` (512×512
physical pixels for the smaller export) from `logicalSize` (1024×1024 world
units). The `@2x` file remains 1024² physical pixels: its actual selected density
is **1 source pixel per world unit**, not native 2× detail. The reader and export
report derive that density explicitly. No PNG was enlarged or regenerated for
this sampling promotion.

The 512-unit high-density alternative is preserved: set `logicalSize` equal to
`rasterSize` (512×512) in the same manifest, leaving all source/output fields
unchanged. `--check` verifies exactly the same files at that alternative; their
actual density is then 2 source pixels per world unit. The Python guard exercises
both declarations and verifies byte-identical raster files.

The renderer emits self-contained material-sized BoardPieces, not one large
source per 32px game cell. Identical blocks share markup and one asset URL across
SVG and GPU paths. Edge blocks clip without stretching source UVs. At 81×51 cells
the selected base is six blocks with four distinct pictures, each at most 1024²
logical units. A GPU backend baking at display 2× may enlarge these pictures; that
does not create new source detail.

The current diagnostic defaults to `ground-runtime-1024` and includes the entire
22×14 c01-02 field (704×448), real trees, structures and unit pictures. It also
shows the 512 high-density alternative and the same camera crop at display 1×/2×.
Field-only crops must match pixel-for-pixel. Composite crops report small offline
antialiasing differences from legacy clipped props. These are offline samples,
not browser/GPU playback.

The first 512-period evidence remains untouched in `ground-runtime`, including
the visible large-plane break at x512 and the original comparison. The selected
1024 period avoids a repeat inside this 704px map and preserves quieter broad
planes. **Larger fields may still expose the non-periodic master at x1024 or
y1024**; this does not certify seamless art. Oversized buildings and legacy river,
road, shore and bridge art remain visible and unchanged.

## Static transparent master exports

`source-overrides.json` is the explicit source owner for remade assets. The existing final manifests remain owners of runtime identity, dimensions and anchors. This first increment supports static `battle-prop` records only; other categories are rejected rather than guessed.

Register each new imagegen PNG under its campaign pack's `masters/remake/` directory, then add its stable topic ID:

```json
{
  "schemaVersion": 1,
  "assets": {
    "C01-PROP-01": {
      "source": "packages/story-candidate-01/assets/final-fantasy-v1/masters/remake/prop-01.png",
      "sha256": "the actual SHA-256 of the source file bytes"
    }
  }
}
```

The example ID is illustrative; select the actual topic from the final manifest. No credentials, private paths or external download URLs belong in this catalog. Keep review/approval provenance in the rebuild inventory, not in the image export contract.

Run from the repository root with Python 3 and the existing Pillow dependency:

```sh
python3 tools/art/export_overrides.py
python3 tools/art/export_overrides.py --check
python3 -m unittest discover -s tools/art -p 'test_*.py' -v
```

The exporter validates all registered inputs before writing and touches only their declared runtime destinations. It never calls the old whole-library builders or needs the missing historical `manifest-complete.json`. An empty catalog is a no-op. Final manifests, gameplay data, anchors and unregistered outputs remain unchanged.

For each prop, it fits the **whole authored canvas** proportionally into a 64×64 RGBA canvas with two pixels of margin and the existing logical center anchor `[32,32]`. If the pack declares an HD counterpart, it also produces 128×128 with four pixels of margin. This is native deterministic export, not creative editing: no inferred crop, background removal, chroma key, thresholding, recoloring or alpha replacement. LANCZOS resampling necessarily interpolates the original color/alpha samples. The source bytes are never changed. Author the subject at the desired canvas location and scale; correcting poor source framing requires a new generated master, not a hidden export crop.

Inputs require a transparent exterior, some zero alpha and a near-opaque subject (alpha ≥240). The outermost edge may contain at most alpha 1/255: this one-step residue changes a composite channel by at most one 8-bit level, and is preserved without modification. Any edge alpha ≥2 is rejected for review, including clipped shadows. The export report records edge maximum, nonzero edge count and the near-opaque core bounding box for both source and output. PNG RGBA output and the same checks are required after fitting. These checks reject empty/opaque/clipped inputs but cannot detect painted checkerboards, bad silhouettes or every fringe. Review each output on light/dark backgrounds and at actual runtime scale. Do not equate successful export with artistic or in-game acceptance.

`--check` compares decoded output pixels and dimensions against current source derivation without writing. After any legacy regeneration, run this exporter and check again: legacy scripts do not know these overrides and can overwrite their destinations. The two commands are the explicit final overlay stage, not competing manually maintained runtime sources. Commit the master, catalog entry and both derived outputs together. The JSON report lists source and pixel hashes for reproducibility within the same Pillow version; resampling may differ across Pillow versions.

Future sprite-sheet and environment contracts should be added only with their real consumer requirements and tests. Do not flatten animated or connected assets into this static format.

Static icon categories are also supported across all three campaigns: equipment
and skill are 48×48 with anchor [24,24]; status and hud are32×32 with anchor
[16,16]. Each uses its own runtime/category directory and no HD entry. Sources
must satisfy the same genuine-alpha contract. Whole authored canvas fitting and
2px transparent padding remain deterministic; tiny icons still need visual review.

## Environment module cell overrides (separate from canonical topics)

Run `python3 tools/art/export_environment_overrides.py` then the same command with
`--check`. The supported module atlases have these exact manifest contracts:

- forest-temperate, forest-existing, scenery-regional: 4×4 / 16 slots, 96²/192² cells,
  384²/768² baseline atlases, default anchor [48,91]
- crossings-fortifications: 4×4 / 16 slots, 96²/192² cells, 384²/768² baseline
  atlases, default anchor [48,91]. Both naming arrays and any explicit cell
  indices must preserve the exact shipped bridge/wall slot order. Bridge/wall
  classification, footprint and obstruction metadata are retained unchanged.
- rural-life: 4×4 / 16 slots, 112²/224² cells, 448²/896² baseline atlases, default anchor [56,108]
- decals-small: 6×4 / 24 slots, 64²/128² cells, 384×256 / 768×512 baseline atlases,
  default anchor [32,60]
- camps-foundations: 4×4 / 16 slots, 144²/288² cells, 576²/1152² baseline atlases,
  default anchor [72,139]
- cliff-modules-temperate: 4×4 / 16 slots, 128²/256² cells, 512²/1024² baseline
  atlases, default anchor [64,127]. Its fixed legacy `cellOrder` short labels are
  checked against the exact semantic `cells[].id` pair at each index. Register
  overrides by the full semantic ID, such as `temperate-hill-cap`, never `hill-cap`.
  The exporter preserves both naming arrays and all elevation/ramp/footprint
  metadata. It does not support or regenerate the separate `elevation-auto-*`
  NESW mask atlases.

All use `tileMode: modules`. Packing, unchanged-cell checks, and review measurements
use the validated atlas column count, including non-first-row six-column cells.
The shared `cell_box(index, size, columns=4)` helper retains its four-column default
for existing callers; atlas-aware callers pass the manifest's actual columns.

Cliff module sources must be complete independently authored RGBA subjects. The
legacy 1254² master is not an exact 4×4 integer-cell sheet: some subjects cross
the nominal quarter boundaries, and its old alpha-bounding-box fit can include
neighbour fragments. Do not use those clipped slots as new master sources or
infer a source foot from their alpha bounds. Review the full original subject
and register one full canvas with an explicit source foot, using the same export
path as other modules. Unselected baseline cells remain pixel-identical.

Fortification modules use that same full-canvas registration. The renderer draws
walls by their registered foot, but existing vertical bridges use `lying: true`:
their cell centre [48,48], not the registered foot, meets the bridge-run midpoint.
Review bridge art against that centre contract; this exporter does not reinterpret
bridge orientation, change terrain topology, or replace the separate horizontal
crossing source. Inspect whole source subjects before authoring: the old gatehouse
and horizontal wooden bridge extend into neighbouring quarter slots.

Keep immutable copies of the original runtime atlases under the pack masters
directory before first export. Register their byte SHA256 as baselines; never
replace them with overridden outputs. Wrong cell dimensions, density, default
anchor, grid, or baseline dimensions are rejected rather than inferred.

`tools/art/environment-overrides.json` uses schemaVersion 2. Each registered cell
contains its source path/hash and one explicit full-canvas registration:

```json
{"schemaVersion":2,"atlases":{"forest-temperate":{
  "baselines":{
    "1x":{"source":"PACK/masters/environment/baseline.png","sha256":"SHA256"},
    "2x":{"source":"PACK/masters/environment/baseline@2x.png","sha256":"SHA256"}
  },
  "cells":{"oak-ancient":{
    "source":"PACK/masters/approved-v5/environment/forest-temperate/oak-ancient/oak-ancient-master.png",
    "sha256":"SHA256",
    "registration":{"scale":0.0685805422647528,"origin":[5,5],"sourceFoot":[640,1150]}
  }}
}}}
```

Replace PACK with the repository-relative campaign pack directory. `scale` is
logical pixels per source pixel; `origin` is the canvas top-left in logical
pixels, using half-pixel increments; `sourceFoot` is the reviewed ground contact
in source pixels. The complete source is resized once at density 2, placed on the
manifest-sized HD transparent cell, and downsampled independently to its logical
cell size before atlas packing. The exporter projects sourceFoot
through that same rounded raster transform into the manifest cell anchor. It
rejects a transform that would clip the full canvas. No alpha-bounds fitting,
source crop, thresholding, recoloring, or repainting is performed.

The approved-v5 forest masters use the complete 1254² canvas at 86 logical
pixels, starting at [5,5]. Roots and fronds may extend below their reviewed
contact. Other atlases' registered cells retain their previous pixels and [48,91] pivots
through equivalent explicit registrations; `offsetY2x` is no longer supported.

The manifest owns each cell's runtime anchor and optional positive
`presentationScale`. The scene renderer multiplies presentationScale by its
placement scale exactly once, around that anchor. Raster registration never
uses presentationScale. Footprints, obstruction, identity, cell order, and all
other gameplay metadata remain unchanged. The atlas-level default anchor stays
unchanged; registered cells supply their own anchors, including rural cells
whose reviewed contact differs from [56,108].

Rural entries use this same schema and algorithm. The current remake targets
wheat-sheaves (slot 4), wheat-bundles (5), grain-sacks-baskets (6), and
farm-handcart-tools (14), using the manifest's existing identities/order. Adding
exporter support does not register or replace any of these cells; their reviewed
sources, registrations, immutable baselines, and resulting exports must be
committed together in a separate binding change.

Decal and camp support is likewise contract-only: it does not register cells,
modify a source atlas, or change any scene consumer. These atlases use the same
full-canvas registration and per-cell sourceFoot projection. Their footprints,
obstruction, presentationScale and other semantic metadata are retained; only a
registered cell's anchor may change to its explicitly projected contact. Future
bindings must supply reviewed per-subject sources and immutable baseline hashes.

The adapter overlays registered cells onto checksummed baselines, preserving
unregistered slices exactly. Reports include per-cell pixel hashes, source
hashes/alpha measurements, registration, logical anchor, and presentationScale.
All inputs validate before writes. `--check` verifies both pixels and projected
anchors without writing. Run the overlay after legacy builds.

For a native-size diagnostic using actual renderer markup:

```sh
node_modules/.bin/vite-node tools/art/review_environment_runtime.ts
python3 tools/art/render_environment_review.py docs/art-rebuild-20261019/approved-v5/environment-runtime
```

Add `--second` to the TypeScript command to review grove, edge, hollow stump,
and fallen log; pass the resulting `environment-runtime-second` directory to
the Python command. Add `--third` and use `environment-runtime-third` for dark
bramble, berry bramble, leaf pile, and boulder-ferns, with the prior oak as a size
reference. Leaf pile is explicitly labeled a catalog-only draw at placement
scale 1 because no shipped scene currently selects it; no production selector
is changed by the diagnostic. The default set remains oak, dense forest, fern,
and boulder. Add `--fourth` and use `environment-runtime-fourth` for autumn
forest, sapling/rock cluster, low stump, and the explicitly reconstructed forest
floor cluster. All 16 temperate slots now have approved-v5 registrations; leaf
pile remains catalog-only until a scene selects it.

Both diagnostic tools use `candidate_review_viewport.ts` for the candidate grid,
tile unit, and scene profile. This keeps viewport construction in one owner;
`extract_calibration_scene.ts` and `review_environment_runtime.ts` consume it.

The SVG and PNG place the four assets beside the runtime swordsman, and the JSON
records actual sampled transforms and alpha-128 silhouette measurements. Shared
viewport normalization makes Inkscape honor the same nested atlas clipping as
the browser. This is an offline contact/scale diagnostic, not browser playback
or a production gameplay screenshot; map-level occlusion remains a separate QA
gate. Ground, routes, and connectivity masks are outside this adapter's scope.

## Framed units and structure states

`python3 tools/art/export_frame_overrides.py [--check]` reads the separate
`tools/art/frame-overrides.json`. The initial catalog is empty. Example topic:

```json
{"schemaVersion":1,"assets":{"C01-UNIT-SWORDSMAN":{
  "kind":"frames-v1",
  "frames":{
    "idle":{"source":"PACK/masters/remake-20261019/combat-unit/unit-swordsman/idle/master.png","sha256":"SHA256"},
    "walk-a":{"source":"PACK/masters/remake-20261019/combat-unit/unit-swordsman/walk-a/master.png","sha256":"SHA256"},
    "attack":{"source":"PACK/masters/remake-20261019/combat-unit/unit-swordsman/attack/master.png","sha256":"SHA256"},
    "walk-b":{"source":"PACK/masters/remake-20261019/combat-unit/unit-swordsman/walk-b/master.png","sha256":"SHA256"}
  },
  "placement":{"renderWidth":30,"origin":[1,1]}
}}}
```

Replace PACK with the complete repository-relative pack path. Placement numbers
are examples, not a reviewed swordsman transform. All authored frame canvases
must have identical dimensions. One shared renderWidth determines aspect-
preserving rounded height; the integer origin applies to all frames. HD doubles
that exact rendered size and origin. Entire fitted canvas must remain inside its
frame. No independent bbox-fitting, frame synthesis, duplication, or reflection
is performed. Shared placement must be selected against the real generated
masters and visible foot contact, not assumed from this example.

Combat semantic keys/order: idle,walk-a,attack,walk-b. Mission: idle,walk-a,
carry-or-work,walk-b. Structures: normal,damaged,captured (states, not a motion
loop). Exact frame sizes/anchors and outputs are checked against manifests.
Each source and exported cell passes alpha QA independently. Identical decoded
source or output frames are rejected, but distinct pixels are NOT evidence of
valid animation: identity, opposing walk steps, attack readability, stable
foot contact, structure state consistency and native-resolution motion preview
must be reviewed separately. The JSON report explicitly leaves semantic review
required. The current renderer has two walk frames at6.25fps and a single attack
pose, not a fluid attack sequence. Baked-in structure characters remain static.

A frame source entry may additionally specify `registrationOffset:[dx,dy]`,
reviewed integer translation in logical pixels, each component between-2 and2.
Default[0,0]. This is distinct from source identity and shared scale; the common
rendered canvas size never changes. Full translated canvas must remain inside
its frame, and the existing per-frame alpha gate still applies. Offsets are
reported per source and export. Use only measured contact/registration fixes,
not automatic pose fitting. Archer walk-a's reviewed[0,1] correction with common
renderWidth30/origin[1,2] preserves its entire30×45 canvas and aligns contact with
idle; it does not change the declared anchor or semantic frame order.

## Opaque surface meadow

`export_surface_overrides.py` handles only the four ordered `surface-meadow`
materials. Catalog: `surface-overrides.json`, schemaVersion1, atlases keyed by
`surface-meadow`, each with `cells` keyed meadow-1 through meadow-4. Each source
has its repository-relative `source` and `sha256`, and lives under pack masters.
Sources must be square opaque PNGs. Every family must be registered together.
The user approved bounded seam matching: independently resize each full source
to64 and32, then average only the outermost row/column across the four tiles.
Interior pixels are unchanged from the corresponding plain resize. No blur,
recolor, source mutation or neighbor-mask change occurs. All four edges match
exactly across the family. The report records edge hashes, changed pixel counts,
maximum channel change and source provenance. `--check` verifies reproducibility;
`--preview-dir PATH` writes real-pixel repeated2×2 and mixed4×4 QA sheets at both
native densities. Exact matching alone does not guarantee visually good seams;
review these sheets and actual scene composites before visual acceptance.

`preview_cohesion.py --root REPOSITORY --output-dir DIRECTORY` builds three
real-export comparison panels: normal/idle, damaged/walk-a, captured/walk-b.
It reads production HD sheets and manifests, keeps declared anchors and layer
order, and records every source hash and authored placement. Native and density2
PNGs are art-review evidence, not gameplay screenshots. Runtime team tint,
shadows, sunlight and animation are explicitly omitted to inspect source art;
these omissions and any non-level-specific placement choices are in the JSON.

`export_transition_overrides.py` supports only `transition-meadow-forest`.
Catalog `transition-overrides.json` has schemaVersion1 and atlases; the named
entry contains `source`, `sha256`, and `baselines` with1x/2x source/hash entries.
All files live under the pack's masters directory. The one generated material
is square and fully opaque. Immutable original runtime atlases supply alpha,
not color: all47 cell alpha channels are copied byte-for-byte, mask order stays
unchanged, and unused cell48 is preserved entirely. The material uses only
independent32/64 resizing plus approved one-pixel self-edge matching. There is
no mask drawing, dilation, blur, alpha extraction, source mutation or palette
adjustment. Run --check and preview_transition.py after export; a matching alpha
hash is a geometry guarantee, not visual transition acceptance.

## Formal reusable area water

The world-material exporter also reads `world-materials-v1/manifest-water-material-v2.json`
with `--manifest`. Its checksummed approved source is resized full-canvas at
512/1024 physical pixels over512logical world units, with only the approved
outermost-pixel average. The initial128logical alternative is retained as evidence. The
separate white/alpha mask atlases contain all 256 eight-neighbour states at32/64
pixels. `water_area.py` owns the previously approved occupancy geometry; both
calibration and formal export use it. The runtime selects masks from actual
water/bridge cells and draws one source image per512logical world block. No
baked c01-02 background, per-cell texture reset, source painting or recolor is used.
`--check` validates source/mask reproducibility. Full native evidence and remaining
repeat seams are documented in `docs/art-rebuild-20261019/approved-v5/water-runtime`.

## Approved dirt-road connector binding

`export_connector_overrides.py [--check]` reads `connector-overrides.json` and
reuses the exact approved22logical core/one-HD-pixel shoulder geometry. Both2x
runtime atlases match the approved export hashes; the edge companion stays blank.
All16NESW masks ×4variants and the existing runtime atlas dimensions/paths remain.
The1x sheet is derived by independently resizing each64px cell to32px before
packing, avoiding the old whole-sheet cross-slot bleed. Only the outermost RGB
pixels of open ports are then averaged against existing opposing open-port
pixels; every alpha and interior pixel is unchanged. Reports include delta and
pixel hashes. Every opposing port pair is exactRGBA at1x and2x. The previous
contaminated1x pair is retained with evidence in `road-runtime/previous-whole-sheet-1x`.

## Registered horizontal crossing

`export_crossing.py [--check]` reads the typed runtime binding's
`registered-crossings-v1/manifest-crossing-v1.json`. It exports only the full
approved transparent canvas, with the reviewed source contact and scale, at
independently rounded1x/2x densities. Source and both approved output hashes are
checked before writes. The runtime places that contact at the existing horizontal
run midpoint, without changing terrain/run logic or scaling to span length.
The old vertical crossings/fortifications atlas stays byte-identical, and the
interactive three-state bridge topic remains separate. No authored vertical
counterpart exists yet. Full-map native evidence is in `crossing-runtime`.

## Sparse authored ground edges

`export_edge_decoration.py [--check]` reads the runtime owner's
`edge-decoration-v1/manifest-edge-decoration-v1.json`. Two immutable approved
transparent sources receive a small transparent canvas margin and independent
whole-canvas 1x/2x resizing. The compensating source anchor preserves world
registration; no source pixel is cropped, painted, masked or recolored.
The margin retains Lanczos support around faint authored perimeter pixels.

The manifest owns six grass-edge and eight bank-edge placements on c01-02 only.
Each has a frozen local terrain precondition covering its full canvas plus a
one-cell halo. Runtime terrain disagreement suppresses that placement; the
precondition cannot write gameplay state. Shared whole-image BoardPieces remain
in the ground layer without standing depth. Other levels have no new coverage.
Road/water geometry dependencies are checksummed so an export requires renewed
layout review after those contracts change.

With an explicit `GROUND_REVIEW_OUTPUT` directory,
`GROUND_REVIEW_LAYER=edges vite-node tools/art/review_ground_runtime.ts` exports
actual field pieces and safety masks. `render_ground_review.py DIRECTORY`
checks road cores, bridge landings, initial feet and the 13-logical-unit near-bank
band at both densities. Meaningful coverage starts at alpha 2/255; authored
alpha-1 residue is retained. Fractional whole-image independent rasterization
allows at most 2/255 alpha and premultiplied-color crop difference; existing
opaque material crop checks remain exact. A deliberate one-pixel shift fails.
With an explicit `STANDING_REVIEW_OUTPUT` directory,
`STANDING_REVIEW_COMPARISON=edges vite-node tools/art/review_standing_depth.ts`
exports the full production viewport with/without only these fourteen patches,
both with shared standing depth. Use `render_ground_review.py DIRECTORY
--scene-only` to rasterize it. Evidence and remaining repeated-bank visual debt
are recorded in `docs/art-rebuild-20261019/approved-v5/edge-runtime/README.md`.

### Large landmark modules

`export_environment_overrides.py` also accepts the exact shipped `landmarks-large`
4×4 module contract:160×160 logical cells,320×320 HD cells, default anchor[80,155].
Each override retains its full authored canvas and records a projected contact;
footprint, obstruction, slot order and unrelated baseline cells remain unchanged.
These compound catalog landmarks are independent of the interactive three-state
structure topics. Export support does not add a scene selector or override an
existing level's denied-atlas policy. The module tests cover mixed geometry,
unchanged source bytes/other cells, metadata preservation and invalid dimensions.

## Gray Flag narrative character-card compositions

Only existing `story-candidate-01` manifest records with category
`narrative-static`, kind `character-card`, and size192×224 are supported. The
manifest owns the topic ID, kind, size and runtime path. A registered card has
this distinct entry in the same `source-overrides.json` catalog:

```json
{
  "kind": "character-card-composite-v1",
  "character": {
    "source": "packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/narrative/leia-01/character.png",
    "sha256": "actual character PNG file SHA-256"
  },
  "background": {
    "source": "packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/narrative/leia-01/background.png",
    "sha256": "actual independently authored background PNG file SHA-256"
  },
  "placement": { "renderWidth": 192, "origin": [0, 16] }
}
```

Place that object under the exact owned topic ID, e.g. `C01-CHAR-LEIA-01`.
Paths/hashes above are an unregistered schema example, not existing approved
masters. The example placement fits a square character canvas at192² within the
192×224 card. `renderWidth` and `origin` are integer output pixels; the entire
character canvas is uniformly resized, preserving aspect up to nearest-pixel
height rounding. Every canvas edge must fit inside the output. No alpha-bbox
crop, flip, rotation, recoloring, background removal or source rewrite occurs.

The character must be genuine RGBA with transparent exterior and near-opaque
subject, following the existing alpha0/edge≤1 contract. Native RGB/opaque RGBA
character masters are rejected. A separate background must be RGB or fully
opaque RGBA. Center-cover resampling fills the card; the report records its exact
source crop box, independent source hashes/sizes/modes, character alpha QA and
placement. The result is an **opaque RGBA UI composition**, not evidence that a
transparent character master was authored correctly. Keep the original RGBA
character file as the reusable source. Human review must reject faux alpha,
wrong identity or an isolated prop accidentally registered as a character; pixel
validation cannot establish semantics.

Commands are the existing `export_overrides.py` / `--check`. All registered inputs
are validated before any output writes. Existing static icon/prop registrations
retain their original schema, pixels, alpha QA and reports. No narrative source,
background or production catalog entry is included in this tooling change.
There is no scene/key-prop reclassification, HD contract or UI change.

Verify the new source/composition in the real dialogue92×112, mobile64×88,
roster44², and96×112 HUD slice consumers before publishing. Preserve named hero
identity instead of copying an incompatible generic battle face; see
`docs/art-rebuild-20261019/approved-v5/narrative-family-audit/hero-identity-contract.md`.
Run focused fixtures with:

```sh
python3 -m unittest discover -s tools/art -p 'test_export_overrides.py' -v
python3 -m unittest discover -s tools/art -p 'test_narrative_card_overrides.py' -v
```
