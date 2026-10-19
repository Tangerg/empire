# Registered transparent master exports

## Approved broad field material

`packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-field-material-v1.json`
owns the approved field source/checksum, the exact replaced atlas/tone, logical
sampling period, world origin, uncropped fit and both runtime paths. The typed
`candidate-01-field-material.ts` reader and `export_field_material.py` consume
that same declaration. This binding replaces only `surface-meadow` tone 0;
the old atlas remains intact and hill tone 2 retains its original fit.

```sh
python3 tools/art/export_field_material.py
python3 tools/art/export_field_material.py --check
node_modules/.bin/vite-node tools/art/review_ground_runtime.ts
python3 tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/ground-runtime
```

The immutable approved grass master is resized as a complete canvas to 512²
and 1024². Only the opposite outermost rows/columns are matched, using the
previously approved 1px seam operation. Interior pixels, palette and composition
are not painted, blurred or recolored. Reports include source, interior and
edge hashes; `--check` verifies reproducible output without writing.

Runtime samples a 512×512 logical period from world origin (0,0), with explicit
inset 0 and bleed 0. It emits self-contained material-sized BoardPieces, not
one large source per 32px game cell. Identical blocks share markup and one HD
asset URL across the SVG and GPU texture paths. Edge blocks clip the viewport
without changing source dimensions. At 81×51 cells the base is 24 blocks with
four distinct pictures, each no larger than 512×512 logical units.

The diagnostic includes the entire 22×14 c01-02 field (704×448), real trees,
structures and unit pictures, and camera crops crossing x512 at native 1×/2×.
It is offline renderer evidence, not browser/GPU playback. Field-only crops
match pixel-for-pixel. Composite crops record small offline antialiasing
differences from legacy clipped props instead of claiming exact matches.

The visual repeat gate is **pending**: the approved master's large planes do
not wrap continuously at x512, despite matching outer pixels. A separately
labeled 1024-period diagnostic uniformly scales the same field picture, keeps
origin and upper layers unchanged, and avoids a repeat within this particular
704px map. It is not an approved runtime selection; its 2× export is an enlarged
diagnostic, not newly authored HD art. Larger maps would still encounter the
repeat. Water, roads, shore masks and all other terrain material bindings remain
unchanged in this increment.

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

## Environment forest cell overrides (separate from canonical topics)

Run `python3 tools/art/export_environment_overrides.py` then the same command with
`--check`. The supported module atlases are forest-temperate, forest-existing,
and scenery-regional. Keep immutable copies of the original 384² and 768²
runtime atlases under the pack masters directory before first export. Register
their byte SHA256 as baselines; never replace them with overridden outputs.

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
in source pixels. The complete source is resized once at density 2, placed on a
192² transparent cell, and downsampled to 96². The exporter projects sourceFoot
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
other gameplay metadata remain unchanged. The atlas-level default anchor is
still [48,91]; registered cells supply their own anchors.

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
