"""Validate lossless pixel fixtures; encoding alone does not establish capture provenance."""
from hashlib import sha256
from io import BytesIO
from pathlib import Path

import numpy as np
from PIL import Image


def read_native_rgba_png(path: Path):
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"NOT_LOSSLESS_RGBA_PNG: wrong file magic: {path}")
    # Hash and decode the same byte snapshot, not two independently read file versions.
    with Image.open(BytesIO(data)) as image:
        if image.format != "PNG" or image.mode != "RGBA":
            raise ValueError(f"NOT_LOSSLESS_RGBA_PNG: wrong decoded format/mode: {path}")
        pixels = np.array(image)
        pixels.setflags(write=False)
        return pixels, {"path": path.name, "format": image.format, "mode": image.mode,
                        "sha256": sha256(data).hexdigest()}
