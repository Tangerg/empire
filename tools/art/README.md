# Registered transparent master exports

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
`--check`. Only forest-temperate and forest-existing module atlases are supported.
Keep immutable copies of the original 384² and768² runtime atlases under the pack
masters directory before first export. Register their byte SHA256 as baselines;
do not replace those baseline files with already-overridden outputs.

`tools/art/environment-overrides.json` uses this structure:

```json
{"schemaVersion":1,"atlases":{"forest-temperate":{
  "baselines":{
    "1x":{"source":"packages/story-candidate-01/assets/final-fantasy-v1/masters/remake-20261019/environment/baselines/forest-temperate.png","sha256":"SHA256"},
    "2x":{"source":"packages/story-candidate-01/assets/final-fantasy-v1/masters/remake-20261019/environment/baselines/forest-temperate@2x.png","sha256":"SHA256"}
  },
  "cells":{"oak-ancient":{"source":"packages/story-candidate-01/assets/final-fantasy-v1/masters/remake-20261019/environment/forest-temperate/oak-ancient/master.png","sha256":"SHA256"}}
}}}
```

Each raw master is fitted as its **complete authored canvas** inside172²,
bottom-centered on192² with bottom at182, then downsampled to96². Unlike the
legacy builder's nonzero-alpha-bbox crop, this deliberately preserves authored
framing and cannot magnify stray alpha1 residue into a crop decision. It does
not clean, threshold, recolor, or repaint source pixels. Generation should use
roughly5–10% margins; inspect apparent root/contact placement and canopy scale
before accepting. Declared anchor remains[48,91] /[96,182].

The adapter overlays only registered cells onto checksummed baselines, preserving
all other atlas slices exactly. Output report contains per-cell before/after
pixel hashes and source hashes/alpha measurements. Manifest IDs, order, anchors,
footprints and gameplay metadata stay unchanged. All inputs validate before any
writes. Ground, route connectivity and masks are outside this adapter's scope.
Run this overlay after legacy builds. `--check` proves reproducibility, not visual
acceptance: actual game screenshots and occlusion review remain separate gates.

Cell entries may explicitly declare `offsetY2x` (integer -10 through10, default0)
for reviewed root/contact placement. This translates the entire fitted canvas
vertically without cropping it, changing scale, or altering alpha. The declared
anchor stays fixed; placement is reported in provenance. It is never inferred
from alpha bounds. Initial reviewed masters need +8 for oak-ancient and +10 for
mixed-forest-dense to bring their visible root bases back near logical y91.
