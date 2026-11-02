"""Encoding negatives protect numerical GPU reports from lossy screenshot substitutions."""
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

from PIL import Image
from native_image_oracle import read_native_rgba_png


class NativeImageOracleChecks(unittest.TestCase):
    def test_encoding_and_snapshot(self):
        with TemporaryDirectory(prefix="empire-native-image-") as folder:
            root = Path(folder)
            good = root / "capture.png"
            Image.new("RGBA", (2, 2), (11, 22, 33, 44)).save(good)
            pixels, metadata = read_native_rgba_png(good)
            self.assertEqual(metadata["mode"], "RGBA")
            self.assertEqual(pixels[0, 0].tolist(), [11, 22, 33, 44])
            self.assertFalse(pixels.flags.writeable)
            fake = root / "actually-jpeg.png"
            Image.new("RGB", (2, 2)).save(fake, format="JPEG")
            with self.assertRaisesRegex(ValueError, "wrong file magic"):
                read_native_rgba_png(fake)
            rgb = root / "opaque-rgb.png"
            Image.new("RGB", (2, 2)).save(rgb)
            with self.assertRaisesRegex(ValueError, "wrong decoded format/mode"):
                read_native_rgba_png(rgb)


if __name__ == "__main__":
    unittest.main()
