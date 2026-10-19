"""Production water uses exactly the approved reusable calibration masks."""
import hashlib
import unittest
import tempfile
import json
from pathlib import Path
from render_ground_review import validate_water_render
from PIL import Image
from export_field_material import ROOT, PACK, export, plan
from water_area import water_area_mask, water_period

MANIFEST = PACK / 'world-materials-v1/manifest-water-material-v2.json'


class WaterMaterialTests(unittest.TestCase):
    def test_production_source_graph_and_reproducibility(self):
        reports = export(ROOT, ROOT / MANIFEST, True)
        self.assertEqual([r['size'] for r in reports], [[128, 128], [256, 256], [512, 512], [1024, 1024]])
        self.assertEqual(reports[1]['pixelsPerWorldUnit'], [2, 2])
        source = ROOT / reports[0]['source']
        self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), reports[0]['sourceSha256'])

    def test_all_exported_alpha_masks_exactly_match_approved_geometry(self):
        planned = plan(ROOT, ROOT / MANIFEST)
        for _, atlas, report in planned[2:]:
            size = report['exportDensity'] * 32
            for mask in range(256):
                x, y = mask % 16 * size, mask // 16 * size
                cell = atlas.crop((x, y, x + size, y + size))
                self.assertEqual(cell.getchannel('A').tobytes(), water_area_mask(size, mask).tobytes())
                for channel in cell.split()[:3]:
                    self.assertEqual(channel.getextrema(), (255, 255))

    def test_raster_matches_approved_resize_and_only_outermost_pixel_correction(self):
        for _, output, report in plan(ROOT, ROOT / MANIFEST)[:2]:
            with Image.open(ROOT / report['source']) as source:
                resized = source.convert('RGB').resize(output.size, Image.Resampling.LANCZOS)
                box = (1, 1, output.width - 1, output.height - 1)
                self.assertEqual(output.crop(box).tobytes(), resized.crop(box).tobytes())
                self.assertEqual(output.tobytes(), water_period(source, output.width).tobytes())
            self.assertEqual(output.crop((0, 0, output.width, 1)).tobytes(), output.crop((0, output.height - 1, output.width, output.height)).tobytes())

    def test_actual_runtime_occupancy_and_uv_guard_rejects_mutations(self):
        evidence = ROOT / 'docs/art-rebuild-20261019/approved-v5/water-runtime'
        provenance = json.loads((evidence / 'runtime-provenance.json').read_text())
        self.assertTrue(validate_water_render(evidence, provenance)['native2xAlphaExact'])
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            with Image.open(evidence / 'field-full@2x.png') as opened:
                original = opened.convert('RGBA')
            for pixel, error in [((0, 0, 0, 0), 'alpha'), ((1, 2, 3, 255), 'RGB')]:
                image = original.copy()
                image.putpixel((680, 100), pixel)
                image.save(target / 'field-full@2x.png')
                with self.assertRaisesRegex(ValueError, error):
                    validate_water_render(target, provenance)
