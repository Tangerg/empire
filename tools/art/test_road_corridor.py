"""Rasterize production SVG masks; independently test the approved road contract."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image
from scipy.ndimage import label

from svg_viewports import NS, flatten_viewports

ROOT = Path(__file__).resolve().parents[2]


def render_fixtures(directory):
    for name in ('patterns', 'actual', 'apron', 'ring', 'bridge', 'seam'):
        svg = ET.parse(directory / f'{name}.svg').getroot()
        flatten_viewports(svg, ET.SubElement(svg, f'{{{NS}}}defs'))
        flattened = directory / f'{name}-flat.svg'
        ET.ElementTree(svg).write(flattened)
        for density in (1, 2):
            subprocess.run(['inkscape', str(flattened), f'--export-width={int(svg.get("width")) * density}',
                            f'--export-filename={directory / name}@{density}x.png'], check=True,
                           env={**os.environ, 'INKSCAPE_PROFILE_DIR': '/tmp/empire-road-corridor-inkscape'},
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def occupancy(data, density):
    size = 32 * density
    result = np.zeros((data['height'] * size, data['width'] * size), dtype=bool)
    for x, y in data['cells']:
        result[y * size:(y + 1) * size, x * size:(x + 1) * size] = True
    return result


def assert_safe_coverage(alpha, data, density):
    size = 32 * density
    yy, xx = np.mgrid[:size, :size]
    radius = np.hypot((xx + .5) / density - 16, (yy + .5) / density - 16)
    # A native pixel touching the disk boundary may include outside-of-disk AA.
    # Every pixel entirely inside the radius8 disk must be fully opaque at both densities.
    interior = radius + np.sqrt(2) / (2 * density) <= 8
    cells = set(map(tuple, data['cells']))
    for x, y in cells:
        if not np.all(alpha[y * size:(y + 1) * size, x * size:(x + 1) * size][interior] == 255):
            raise AssertionError(f'center safety disk lost opaque interior at {(x, y)}')
        for dx, dy in ((1, 0), (0, 1)):
            if (x + dx, y + dy) not in cells:
                continue
            port = (alpha[y * size + 5 * density:y * size + 27 * density, (x + 1) * size - 1:(x + 1) * size + 1]
                    if dx else alpha[(y + 1) * size - 1:(y + 1) * size + 1, x * size + 5 * density:x * size + 27 * density])
            if not np.all(port == 255):
                raise AssertionError(f'22px cardinal port lost opacity at {(x, y, dx, dy)}')
    for x, y in data['excluded'] + data['protectedCells']:
        if np.any(alpha[y * size:(y + 1) * size, x * size:(x + 1) * size]):
            raise AssertionError(f'paint entered excluded cell {(x, y)}')
    outside = (alpha > 0) & ~occupancy(data, density)
    yy, xx = np.nonzero(outside)
    if len(xx):
        px, py = (xx + .5) / density, (yy + .5) / density
        distances = np.full(len(px), np.inf)
        for x, y in cells:
            dx = np.maximum(np.maximum(x * 32 - px, px - (x + 1) * 32), 0)
            dy = np.maximum(np.maximum(y * 32 - py, py - (y + 1) * 32), 0)
            distances = np.minimum(distances, np.hypot(dx, dy))
        # Pixel areas may intersect the exact Euclidean2 boundary by half a diagonal.
        if distances.max() > 2 + np.sqrt(2) / (2 * density):
            raise AssertionError('paint exceeded the geometric2px shoulder bound')


class RoadCorridorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = None
        if 'ROAD_CORRIDOR_FIXTURES' in os.environ:
            cls.directory = Path(os.environ['ROAD_CORRIDOR_FIXTURES'])
        else:
            cls.temp = tempfile.TemporaryDirectory(prefix='empire-road-corridor-')
            cls.directory = Path(cls.temp.name)
            subprocess.run([str(ROOT / 'node_modules/.bin/vite-node'), 'tools/art/review_road_corridor.ts'], cwd=ROOT,
                           env={**os.environ, 'ROAD_CORRIDOR_OUTPUT': str(cls.directory)}, check=True)
            render_fixtures(cls.directory)
        cls.patterns = json.loads((cls.directory / 'patterns.json').read_text())

    @classmethod
    def tearDownClass(cls):
        if cls.temp:
            cls.temp.cleanup()

    def alpha(self, name, density):
        return np.asarray(Image.open(self.directory / f'{name}@{density}x.png').convert('RGBA'))[:, :, 3]

    def data(self, name):
        return json.loads((self.directory / f'{name}.json').read_text())

    def test_all256_centers_ports_cardinal_components_and_rotation(self):
        for density in (1, 2):
            atlas = self.alpha('patterns', density)
            size = 160 * density
            panels = [atlas[i // 16 * size:(i // 16 + 1) * size, i % 16 * size:(i % 16 + 1) * size] for i in range(256)]
            for mask, alpha in enumerate(panels):
                with self.subTest(density=density, mask=mask):
                    data = self.patterns[mask]
                    assert_safe_coverage(alpha, data, density)
                    cells = np.zeros((5, 5), dtype=bool)
                    for x, y in data['cells']:
                        cells[y, x] = True
                    self.assertEqual(label(alpha >= 128)[1], label(cells)[1])
                    rotated = ((mask << 2) | (mask >> 6)) & 255
                    # Inkscape's coverage AA is directional even for a circle (18/255 at2x).
                    # Exact continuous rotation is independently asserted in the TS test.
                    turned, other = np.rot90(alpha, -1), panels[rotated]
                    delta = turned != other
                    partial = ((turned > 0) & (turned < 255)) | ((other > 0) & (other < 255))
                    self.assertTrue(np.all(partial[delta]), 'rotation changed a non-AA interior pixel')

    def test_actual84_centers99_ports_and_two_open_grass_holes(self):
        data = self.data('actual')
        self.assertEqual(len(data['cells']), 84)
        self.assertEqual(sum(len(chain) - 1 for chain in data['chains']), 99)
        self.assertEqual(data['protectedCells'], [[11, 8], [12, 8]])
        for density in (1, 2):
            alpha = self.alpha('actual', density)
            assert_safe_coverage(alpha, data, density)
            self.assertEqual(label(alpha >= 128)[1], 1)
            self.assertEqual(label(np.pad(alpha < 128, 1, constant_values=True))[1] - 1, 2)
            for x, ys in ((0, (9, 10)), (27 * 32 * density - 1, (6, 7))):
                for y in ys:
                    self.assertTrue(np.all(alpha[(y * 32 + 5) * density:(y * 32 + 27) * density, x] == 255))

    def test_current_only_plain_shoulder_is_cell6_12_and_2_75_square_pixels(self):
        data, alpha = self.data('actual'), self.alpha('actual', 2)
        outside = (alpha >= 128) & ~occupancy(data, 2)
        yy, xx = np.nonzero(outside)
        self.assertEqual(set(zip(xx // 64, yy // 64)), {(6, 12)})
        self.assertEqual(outside.sum() / 4, 2.75)
        # All center sample points in the approved2x evidence are fully opaque.
        yy, xx = np.mgrid[:64, :64]
        disk = (xx + .5 - 32) ** 2 + (yy + .5 - 32) ** 2 <= 16 ** 2
        for x, y in data['cells']:
            self.assertTrue(np.all(alpha[y * 64:(y + 1) * 64, x * 64:(x + 1) * 64][disk] == 255))

    def test_cycles_aprons_bridge_exclusion_and_material_chunk_seam(self):
        for density in (1, 2):
            for name in ('apron', 'ring', 'bridge', 'seam'):
                with self.subTest(density=density, fixture=name):
                    assert_safe_coverage(self.alpha(name, density), self.data(name), density)
            apron = self.alpha('apron', density)
            self.assertTrue(np.all(apron[48 * density:80 * density, 48 * density:80 * density] == 255))
            self.assertEqual(label(np.pad(apron < 128, 1, constant_values=True))[1] - 1, 0)
            self.assertEqual(label(np.pad(self.alpha('ring', density) < 128, 1, constant_values=True))[1] - 1, 1)
            self.assertTrue(np.all(self.alpha('bridge', density)[69 * density:91 * density, 96 * density - 1] == 255))
            self.assertTrue(np.all(self.alpha('seam', density)[69 * density:91 * density, 1024 * density - 1:1024 * density + 1] == 255))

    def test_guards_reject_removed_center_disconnected_port_and_forbidden_paint(self):
        data, original = self.data('actual'), self.alpha('actual', 2)
        for point in ((16, 9 * 32 + 16), (32, 9 * 32 + 16), (11 * 32 + 16, 8 * 32 + 16), (6 * 32 + 16, 12 * 32 + 16)):
            mutated = original.copy()
            x, y = (int(p * 2) for p in point)
            mutated[y, x] = 0 if original[y, x] else 255
            with self.subTest(point=point), self.assertRaises(AssertionError):
                assert_safe_coverage(mutated, data, 2)


if __name__ == '__main__':
    unittest.main()
