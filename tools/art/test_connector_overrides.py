import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_connector_overrides import ROOT, CATALOG, plan, export, downsample_cells, match_open_port_rgb


def cells(image):
    size = image.width // 16
    return [image.crop((i % 16 * size, i // 16 * size, (i % 16 + 1) * size, (i // 16 + 1) * size)) for i in range(64)]


def check_ports(image):
    parts = cells(image)
    size = parts[0].width
    for a in range(16):
        for b in range(16):
            for va in range(4):
                for vb in range(4):
                    first, second = parts[a * 4 + va], parts[b * 4 + vb]
                    if a & 2 and b & 8 and first.crop((size - 1, 0, size, size)).tobytes() != second.crop((0, 0, 1, size)).tobytes():
                        raise ValueError('horizontal port mismatch')
                    if a & 4 and b & 1 and first.crop((0, size - 1, size, size)).tobytes() != second.crop((0, 0, size, 1)).tobytes():
                        raise ValueError('vertical port mismatch')
    for mask in range(16):
        for variant in range(4):
            cell = parts[mask * 4 + variant]
            for bit, point in ((1, (size // 2, 0)), (2, (size - 1, size // 2)), (4, (size // 2, size - 1)), (8, (0, size // 2))):
                if (cell.getpixel(point)[3] > 128) != bool(mask & bit):
                    raise ValueError('port topology changed')


class ConnectorOverrides(unittest.TestCase):
    def test_checksums_and_reproducibility(self):
        reports = export(ROOT, ROOT / CATALOG, True)
        self.assertEqual(len(reports), 4)
        for report in reports:
            self.assertEqual(hashlib.sha256((ROOT / report['source']).read_bytes()).hexdigest(), report['sourceSha256'])
            self.assertEqual(report['roadWidthLogical'], 22)
            if report['density'] == 2:
                self.assertTrue(report['approved2xExportExact'])

    def test_all_ports_and_variants_match_at_both_densities(self):
        for _, _, image, report in plan(ROOT, ROOT / CATALOG):
            if report['atlas'] == 'route-edge-dirt-road':
                self.assertEqual(image.getchannel('A').getextrema(), (0, 0))
                continue
            check_ports(image)
            parts = cells(image)
            for mask in range(16):
                for variant in range(1, 4):
                    self.assertEqual(parts[mask * 4].tobytes(), parts[mask * 4 + variant].tobytes())
            corrupted = image.copy()
            corrupted.putpixel((32 * report['density'] - 1, 16 * report['density']), (1, 2, 3, 255))
            with self.assertRaisesRegex(ValueError, 'topology'):
                check_ports(corrupted)

    def test_isolated_resize_and_edge_match_preserve_every_alpha_and_interior_pixel(self):
        native = plan(ROOT, ROOT / CATALOG)[1][2]
        independent = downsample_cells(native)
        fixed, report = match_open_port_rgb(independent)
        self.assertEqual(fixed.getchannel('A').tobytes(), independent.getchannel('A').tobytes())
        for before, after in zip(cells(independent), cells(fixed)):
            self.assertEqual(before.crop((1, 1, 31, 31)).tobytes(), after.crop((1, 1, 31, 31)).tobytes())
        for original, small in zip(cells(native), cells(independent)):
            self.assertEqual(original.resize((32, 32), Image.Resampling.LANCZOS).tobytes(), small.tobytes())
        self.assertGreater(report['changedPixels'], 0)
        self.assertFalse(report['alphaChanged'])
        # The guard rejects a changed alpha rather than painting over it.
        independent.putpixel((32 * 4 + 16, 0), (1, 2, 3, 0))
        with self.assertRaisesRegex(ValueError, 'alpha'):
            match_open_port_rgb(independent)

    def test_unreviewed_source_or_geometry_cannot_export(self):
        original = json.loads((ROOT / CATALOG).read_text())
        with tempfile.TemporaryDirectory() as directory:
            catalog = Path(directory) / 'catalog.json'
            for key in ('sha256', 'geometrySha256'):
                catalog.write_text(json.dumps({**original, key: '0' * 64}))
                with self.assertRaisesRegex(ValueError, 'SHA-256'):
                    plan(ROOT, catalog)
