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

Inputs require genuine transparent outer edges, some zero alpha and a near-opaque subject (alpha ≥240). PNG RGBA output and the same checks are required after fitting. These checks reject empty/opaque/clipped inputs but cannot detect painted checkerboards, bad silhouettes or every fringe. Review each output on light/dark backgrounds and at actual runtime scale. Do not equate successful export with artistic or in-game acceptance.

`--check` compares decoded output pixels and dimensions against current source derivation without writing. After any legacy regeneration, run this exporter and check again: legacy scripts do not know these overrides and can overwrite their destinations. The two commands are the explicit final overlay stage, not competing manually maintained runtime sources. Commit the master, catalog entry and both derived outputs together. The JSON report lists source and pixel hashes for reproducibility within the same Pillow version; resampling may differ across Pillow versions.

Future sprite-sheet and environment contracts should be added only with their real consumer requirements and tests. Do not flatten animated or connected assets into this static format.
