import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_crossing import ROOT, MANIFEST, export, plan


class RegisteredCrossing(unittest.TestCase):
    def test_native_full_canvas_exactly_matches_approved_export(self):
        reports = export(ROOT, ROOT / MANIFEST, True)
        self.assertEqual([r['size'] for r in reports], [[140, 74], [280, 147]])
        for _, _, output, report in plan(ROOT, ROOT / MANIFEST):
            with Image.open(ROOT / report['source']) as source:
                self.assertEqual(output.tobytes(), source.resize(output.size, Image.Resampling.LANCZOS).tobytes())
            self.assertTrue(report['approvedExportExact'])
            self.assertEqual(report['sourceFoot'], [864, 418])
            self.assertAlmostEqual(report['logicalAnchor'][0], 69.96963123644251)
            self.assertAlmostEqual(report['logicalAnchor'][1], 33.85104844540853)

    def test_rejects_wrong_source_canvas_registration_or_unauthored_orientation(self):
        original = json.loads((ROOT / MANIFEST).read_text())
        with tempfile.TemporaryDirectory() as directory:
            manifest = Path(directory) / 'manifest.json'
            for key, value, message in [('sourceSha256', '0' * 64, 'SHA-256'), ('sourceSize', [96, 96], 'complete'),
                                         ('sourceFoot', [-1, 418], 'contact'), ('orientation', 'vertical', 'horizontal'),
                                         ('logicalPerSourcePixel', .1, 'approved')]:
                manifest.write_text(json.dumps({**original, key: value}))
                with self.assertRaisesRegex(ValueError, message):
                    plan(ROOT, manifest)
                self.assertFalse((manifest.parent / 'runtime').exists())

    def test_writes_only_new_crossing_rasters_and_never_replaces_old_vertical_atlas(self):
        planned = plan(ROOT, ROOT / MANIFEST)
        self.assertEqual(len(planned), 2)
        for path, _, _, _ in planned:
            self.assertEqual(path.parent, ROOT / MANIFEST.parent / 'runtime')
            self.assertNotIn('crossings-fortifications', path.name)
