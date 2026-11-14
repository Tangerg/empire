# N3 third pass source (ignored by Godot importer)

Run into an external staging directory, not this authoring folder:

```sh
C01_N3_OUTPUT=/tmp/c01-n3 python make_surfaces.py
C01_N3_OUTPUT=/tmp/c01-n3 blender -b -t 2 --python build_n3.py
```

The first script authors original physically interpreted albedo/normal tile maps and palette swatches. It requires existing Python NumPy/Pillow. No external images/assets are copied, no directional light/cast shadow is painted into the albedo. The second uses shared original modeling helpers to produce dedicated N3 upper/lower GLBs with embedded textures. Fixed mathematical surfaces and geometry are reproducible; exact container serialization across Blender versions is not promised.

Only inside roof repeating solids are replaced by mapped surfaces. Real beveled ridge caps, continuous eave edges and shopfront/stone details remain. Dedicated IDs matter: do not overwrite generic c01_row_4x2, which remains E1's current module.
