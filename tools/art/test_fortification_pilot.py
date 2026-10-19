"""Raster invariants for actual pilot SVG output; no painted source generation."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

import numpy as np
from PIL import Image
from render_ground_review import rasterize

ROOT = Path(__file__).resolve().parents[2]


def expected_alpha(name, density, top_only=False):
    if name == 'open-ns':
        alpha = np.zeros((96 * density, 96 * density), dtype=np.uint8)
        alpha[:, 28 * density:(60 if top_only else 64) * density] = 255
    elif name == 'open-double':
        alpha = np.zeros((96 * density, 128 * density), dtype=np.uint8)
        alpha[:, 28 * density:(92 if top_only else 96) * density] = 255
    else:
        alpha = np.zeros((80 * density, 96 * density), dtype=np.uint8)
        alpha[16 * density:(48 if top_only else 64) * density, :] = 255
    return alpha


def assert_solid_strips(actual, expected):
    if not np.array_equal(actual, expected):
        raise AssertionError(f'wall interface/coverage differs at {np.count_nonzero(actual != expected)} pixels')


class FortificationPilotTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = None
        if 'WALL_PILOT_FIXTURES' in os.environ:
            cls.root = Path(os.environ['WALL_PILOT_FIXTURES'])
        else:
            cls.temp = tempfile.TemporaryDirectory(prefix='empire-wall-pilot-test-')
            cls.root = Path(cls.temp.name)
            for sampling in (4, 16):
                directory = cls.root / str(sampling)
                subprocess.run([str(ROOT / 'node_modules/.bin/vite-node'), 'tools/art/review_fortification_pilot.ts'], cwd=ROOT,
                               env={**os.environ, 'WALL_PILOT_DENSITY': str(sampling), 'WALL_PILOT_OUTPUT': str(directory),
                                    'WALL_PILOT_FIXTURES_ONLY': '1'}, check=True, stdout=subprocess.DEVNULL)
                rasterize(directory)

    @classmethod
    def tearDownClass(cls):
        if cls.temp:
            cls.temp.cleanup()

    def rgba(self, sampling, name, density):
        return np.asarray(Image.open(self.root / str(sampling) / f'{name}@{density}x.png').convert('RGBA'))

    def test_open_full_caps_and_all_shared_side_ports_are_fully_opaque(self):
        for sampling in (4, 16):
            for density in (1, 2):
                for name in ('open-ns', 'open-ew', 'open-double'):
                    for top_only in (False, True):
                        with self.subTest(sampling=sampling, density=density, name=name, top_only=top_only):
                            alpha = self.rgba(sampling, name + ('-top' if top_only else ''), density)[:, :, 3]
                            assert_solid_strips(alpha, expected_alpha(name, density, top_only))

    def test_dense_material_repeats_at64_world_units_not_each32px_cell(self):
        for density in (1, 2):
            for name in ('open-ns', 'open-double'):
                image = self.rgba(16, name, density)
                np.testing.assert_array_equal(image[:32 * density], image[64 * density:96 * density])
                self.assertFalse(np.array_equal(image[:32 * density], image[32 * density:64 * density]), 'material restarted every cell')
            image = self.rgba(16, 'open-ew', density)
            first, repeated = image[:, :32 * density], image[:, 64 * density:96 * density]
            np.testing.assert_array_equal(first[:, :, 3], repeated[:, :, 3])
            # Overlapping antialiased side cuts can round one native RGB pixel
            # by one channel value; UV coordinates and opaque coverage stay exact.
            self.assertLessEqual(np.abs(first[:, :, :3].astype(int) - repeated[:, :, :3].astype(int)).max(), 1)
            top = self.rgba(16, 'open-ew-top', density)
            np.testing.assert_array_equal(top[:, :32 * density], top[:, 64 * density:96 * density])
            self.assertFalse(np.array_equal(image[:, :32 * density], image[:, 32 * density:64 * density]))

    def test_unrepeated_material_has_no32_or64_cell_stamp(self):
        for density in (1, 2):
            for name in ('open-ns', 'open-double'):
                image = self.rgba(4, name, density)
                for distance in (32, 64):
                    self.assertFalse(np.array_equal(image[:32 * density], image[distance * density:(distance + 32) * density]))

    def test_context_not_fake_end_modules_owns_open_strip_masks(self):
        for sampling in (4, 16):
            for name, masks, count in [('open-ns', {5}, 5), ('open-ew', {10}, 5), ('open-double', {7, 13}, 10)]:
                data = json.loads((self.root / str(sampling) / f'{name}.json').read_text())
                self.assertEqual({c['mask'] for c in data['displayed']}, masks)
                self.assertEqual(len(data['displayed']), count)
                self.assertIn('end states outside window are not implemented', data['boundary'])

    def test_missing_port_shifted_anchor_and_side_seam_mutations_are_rejected(self):
        for density in (1, 2):
            expected = expected_alpha('open-double', density)
            center_gap = expected.copy()
            center_gap[:, 59 * density:61 * density] = 0
            shifted = np.roll(expected, density, axis=1)
            side_seam = expected.copy()
            side_seam[32 * density, 93 * density] = 195
            for mutation in (center_gap, shifted, side_seam):
                with self.assertRaises(AssertionError):
                    assert_solid_strips(mutation, expected)


if __name__ == '__main__':
    unittest.main()
