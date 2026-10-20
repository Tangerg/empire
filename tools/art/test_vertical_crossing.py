import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_vertical_crossing import ROOT, MANIFEST, plan, export


class RegisteredVerticalCrossing(unittest.TestCase):
    def test_complete_independently_rounded_native_exports_and_pivot(self):
        reports = export(ROOT, ROOT / MANIFEST, True)
        self.assertEqual([report['size'] for report in reports], [[154, 154], [307, 307]])
        for _, _, output, report in plan(ROOT, ROOT / MANIFEST):
            with Image.open(ROOT / report['source']) as source:
                self.assertEqual(output.tobytes(), source.resize(output.size, Image.Resampling.LANCZOS).tobytes())
            self.assertLessEqual(report['alpha']['edgeMaxAlpha'], 1)
            self.assertEqual(report['sourceFoot'], [627, 594])
            self.assertAlmostEqual(report['logicalAnchor'][0], 627 * 96 / 784)
            self.assertAlmostEqual(report['logicalAnchor'][1], 594 * 96 / 784)
            self.assertEqual((202 - report['sourceFoot'][1]) * report['logicalPerSourcePixel'], -48)
            self.assertEqual((986 - report['sourceFoot'][1]) * report['logicalPerSourcePixel'], 48)

    def test_rejects_source_crop_changed_density_and_unreviewed_orientation(self):
        original = json.loads((ROOT / MANIFEST).read_text())
        with tempfile.TemporaryDirectory() as directory:
            manifest = Path(directory) / 'manifest.json'
            for key, value, message in [
                ('sourceSha256', '0' * 64, 'SHA-256'), ('sourceSize', [96, 96], 'complete'),
                ('sourceFoot', [-1, 594], 'contact'), ('orientation', 'horizontal', 'vertical'),
                ('logicalPerSourcePixel', .125, 'approved'),
            ]:
                manifest.write_text(json.dumps({**original, key: value}))
                with self.assertRaisesRegex(ValueError, message):
                    plan(ROOT, manifest)
                self.assertFalse((manifest.parent / 'runtime').exists())

    def test_complementary_windows_open_only_reviewed_rail_bands(self):
        document = json.loads((ROOT / MANIFEST.parent / 'manifest-junction-v1.json').read_text())
        horizontal = document['horizontal']['cutouts']
        vertical = document['vertical']['cutouts']
        inside = lambda cuts, x, y: any(rx <= x < rx + w and ry <= y < ry + h for rx, ry, w, h in cuts)
        # The vertical through-deck removes both transverse parapets/arch, while
        # the horizontal 12px deck opens the two vertical side parapets.
        for x in (-12, 0, 12):
            self.assertTrue(inside(horizontal, x, -10))
            self.assertTrue(inside(horizontal, x, 12))
            self.assertFalse(inside(horizontal, x, 0))
        for x in (-24, 24):
            for y in (-5, 0, 5):
                self.assertTrue(inside(vertical, x, y))
            self.assertFalse(inside(vertical, x, -10))
            self.assertFalse(inside(vertical, x, 12))
        for x, y in [(0, -48), (48, 0), (0, 48), (-48, 0)]:
            self.assertFalse(inside(horizontal, x, y))
            self.assertFalse(inside(vertical, x, y))
        for cuts in (horizontal, vertical):
            self.assertEqual(len(cuts), 2)
            self.assertTrue(all(w < 32 or h < 32 for _, _, w, h in cuts))
