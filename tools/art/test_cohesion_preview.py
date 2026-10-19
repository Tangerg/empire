import unittest
from PIL import Image
from preview_cohesion import place


class CohesionPreviewTests(unittest.TestCase):
    def test_anchor_placement_preserves_unscaled_sprite(self):
        source = Image.new('RGBA', (64, 96))
        source.paste((100, 120, 140, 255), (10, 8, 54, 92))
        before = source.tobytes()
        canvas = Image.new('RGBA', (320, 240))
        report = place(canvas, source, (80, 100), (16, 47))
        self.assertEqual(report['destinationPixels'], [128, 106])
        self.assertEqual(canvas.crop((128, 106, 192, 202)).tobytes(), source.tobytes())
        self.assertEqual(source.tobytes(), before)

    def test_clipped_asset_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'clips'):
            place(Image.new('RGBA', (64, 64)), Image.new('RGBA', (64, 96)), (0, 0), (16, 47))
