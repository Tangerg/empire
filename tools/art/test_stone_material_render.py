from pathlib import Path
import tempfile
import unittest

from PIL import Image

from render_stone_review import compare


class StoneRenderComparisonTests(unittest.TestCase):
    def test_guard_rejects_alpha_rgb_and_size_differences_at_either_density(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for density in (1, 2):
                for name in ('legacy-alpha', 'world-alpha', 'reference-uv', 'world-uv'):
                    Image.new('RGBA', (8 * density, 8 * density), (100, 120, 140, 200)).save(root / f'{name}@{density}x.png')
            self.assertEqual(len(compare(root)), 4)
            for density in (1, 2):
                for name in ('world-alpha', 'world-uv'):
                    path = root / f'{name}@{density}x.png'
                    before = path.read_bytes()
                    for pixel in [(100, 120, 140, 199), (99, 120, 140, 200)]:
                        with Image.open(path) as current:
                            current.putpixel((0, 0), pixel)
                            current.save(path)
                        with self.assertRaisesRegex(ValueError, 'differs'):
                            compare(root)
                        path.write_bytes(before)
                    Image.new('RGBA', (9 * density, 8 * density)).save(path)
                    with self.assertRaisesRegex(ValueError, 'dimensions'):
                        compare(root)
                    path.write_bytes(before)


if __name__ == '__main__':
    unittest.main()
