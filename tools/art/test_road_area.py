"""Road export, bounded topology, continuous UV, and deliberate mutation guards."""
import ast
from collections import deque
import hashlib
import json
from pathlib import Path
import re
import shutil
import tempfile
import unittest

import numpy as np
from PIL import Image

from export_road_material import MANIFEST, PACK, ROOT, SOURCE, SOURCE_SHA256, export, plan
from road_area import SUPERSAMPLE, road_area_mask, road_coverage


NEIGHBORS = ((0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1))
APRON_ANCHORS = ((11, 6), (20, 6), (21, 6), (24, 6), (25, 6), (2, 8), (3, 8),
                 (6, 8), (0, 9), (1, 9), (2, 9), (3, 9), (21, 9), (15, 10))


def occupancy_mask(cell, connections):
    x, y = cell
    return sum(1 << bit for bit, (dx, dy) in enumerate(NEIGHBORS) if (x + dx, y + dy) in connections)


def fixture_alpha(cells, dimensions, density=1, connections=None, masks=None):
    """Reference occupancy assembly, deliberately separate from runtime drawing."""
    connections = cells if connections is None else connections
    size = 32 * density
    result = np.zeros((dimensions[1] * size, dimensions[0] * size), dtype=np.uint8)
    for x, y in cells:
        mask = occupancy_mask((x, y), connections)
        image = np.asarray(road_area_mask(size, mask)) if masks is None else masks[mask]
        result[y * size:(y + 1) * size, x * size:(x + 1) * size] = image
    return result


def assert_exact_alpha(actual, cells, dimensions, density=1, connections=None):
    expected = fixture_alpha(cells, dimensions, density, connections)
    if not np.array_equal(actual, expected):
        raise ValueError('road alpha disagrees with authoritative paint/connection occupancy')


def sample_world_rgb(material, dimensions, origin=(0, 0)):
    """Reference 1px/world-unit sampling; every chunk supplies its absolute origin."""
    width, height = dimensions
    xs = (np.arange(width) + origin[0]) % material.shape[1]
    ys = (np.arange(height) + origin[1]) % material.shape[0]
    return material[ys[:, None], xs[None, :]]


def assert_opaque_rgb(rgba, material, origin=(0, 0)):
    expected = sample_world_rgb(material, (rgba.shape[1], rgba.shape[0]), origin)
    if not np.array_equal(rgba[:, :, :3][rgba[:, :, 3] == 255], expected[rgba[:, :, 3] == 255]):
        raise ValueError('road RGB differs from unchanged world-space source sampling')


def components(binary, diagonal=False):
    remaining = set(zip(*np.nonzero(binary)))
    steps = NEIGHBORS if diagonal else NEIGHBORS[::2]
    count = 0
    while remaining:
        queue = deque([remaining.pop()])
        count += 1
        while queue:
            y, x = queue.popleft()
            for dx, dy in steps:
                neighbor = (y + dy, x + dx)
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    queue.append(neighbor)
    return count


def actual_terrain():
    source = (ROOT / 'packages/story-candidate-01/src/levels.ts').read_text()
    match = re.search(r'const TWIN_HILLS = (\[.*?\]);', source, re.DOTALL)
    if match is None:
        raise ValueError('actual authored c01-01 terrain array was not found')
    return ast.literal_eval(match[1])


class RoadGeometryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.masks = {density: [np.asarray(road_area_mask(32 * density, mask)) for mask in range(256)] for density in (1, 2)}

    def test_input_validation(self):
        for size, mask in ((31, 0), (128, 0), (32, -1), (32, 256), (32, True), (32, 3.0)):
            with self.subTest(size=size, mask=mask), self.assertRaises(ValueError):
                road_area_mask(size, mask)

    def test_all_256_rotations_centers_and_opaque_cardinal_ports(self):
        for density, masks in self.masks.items():
            size = 32 * density
            coordinates = (np.arange(size) + .5) / density
            # Include pixels whose area intersects the protected radius-eight disk.
            near_x = np.maximum(np.abs(coordinates - 16) - .5 / density, 0)
            protected = near_x[None, :] ** 2 + near_x[:, None] ** 2 <= 8 ** 2
            for mask, alpha in enumerate(masks):
                with self.subTest(density=density, mask=mask):
                    self.assertTrue(np.all(alpha[protected] == 255), 'protected center disk is not opaque')
                    rotated = ((mask << 2) | (mask >> 6)) & 255
                    np.testing.assert_array_equal(np.rot90(alpha, -1), masks[rotated])
                    ports = (alpha[0, 5 * density:27 * density], alpha[5 * density:27 * density, -1],
                             alpha[-1, 5 * density:27 * density], alpha[5 * density:27 * density, 0])
                    edges = (alpha[0], alpha[:, -1], alpha[-1], alpha[:, 0])
                    for bit, port, edge in zip((0, 2, 4, 6), ports, edges):
                        if mask & (1 << bit):
                            self.assertTrue(np.all(port == 255), f'cardinal port {bit} is narrower than 22px')
                        else:
                            self.assertTrue(np.all(edge == 0), f'absent cardinal neighbor {bit} gained a connection')

    def test_every_mask_is_one_connected_region_without_islands_or_internal_holes(self):
        for mask, alpha in enumerate(self.masks[1]):
            with self.subTest(mask=mask):
                self.assertEqual(components(alpha >= 2, diagonal=True), 1)
                self.assertEqual(components(np.pad(alpha < 2, 1, constant_values=True)), 1)

    def test_all_valid_neighbor_pairs_have_exact_shared_boundary_coverage(self):
        samples = np.arange(0, 32 * SUPERSAMPLE + 1) / SUPERSAMPLE
        checked = 0
        for a in range(256):
            if not a & (1 << 2):
                continue
            for b in range(256):
                # Both centers are occupied; the four overlapping surrounding
                # cells must describe the same actual occupancy on either side.
                if not b & (1 << 6) or any(bool(a & (1 << left)) != bool(b & (1 << right))
                                          for left, right in ((0, 7), (1, 0), (3, 4), (4, 5))):
                    continue
                np.testing.assert_array_equal(road_coverage(a, 32, samples), road_coverage(b, 0, samples))
                np.testing.assert_array_equal(self.masks[2][a][:, -1], self.masks[2][b][:, 0])
                # At 1x these are different integrated pixel areas beside a
                # tangent arc, not samples of the shared geometric boundary.
                delta = np.abs(self.masks[1][a][:, -1].astype(int) - self.masks[1][b][:, 0].astype(int))
                self.assertLessEqual(int(delta.max()), 8)
                checked += 1
        self.assertEqual(checked, 1024)
        # Ninety-degree equivariance above proves the same for north/south.

    def test_independent_densities_integrate_one_common_world_grid(self):
        coordinates = (np.arange(32 * SUPERSAMPLE) + .5) / SUPERSAMPLE
        for mask in range(256):
            coverage = road_coverage(mask, coordinates[None, :], coordinates[:, None])
            for density in (1, 2):
                size, step = 32 * density, SUPERSAMPLE // density
                count = coverage.reshape(size, step, size, step).sum(axis=(1, 3))
                expected = ((count * 255 + step * step // 2) // (step * step)).astype(np.uint8)
                np.testing.assert_array_equal(self.masks[density][mask], expected)
        self.assertGreaterEqual(SUPERSAMPLE, 8)

    def test_analytic_convex_and_concave_radii(self):
        # Isolated top-left corner has center(14,14), radius9, inset5.
        self.assertFalse(road_coverage(0, 5, 5))
        self.assertTrue(road_coverage(0, 5, 14))
        self.assertFalse(road_coverage(0, 8, 7))
        self.assertTrue(road_coverage(0, 8, 8))
        n_w = (1 << 0) | (1 << 6)
        self.assertFalse(road_coverage(n_w, 2, 2))
        self.assertTrue(road_coverage(n_w, 3, 4))
        self.assertTrue(road_coverage(n_w | (1 << 7), 0, 0))
        for mask in range(256):
            self.assertFalse(np.any(road_coverage(mask, [-.01, 32.01, 16, 16], [16, 16, -.01, 32.01])))

    def test_one_and_two_lane_runs_are_22_and_54_pixels_with_no_internal_seams(self):
        for density in (1, 2):
            for lanes, expected_width in ((1, 22), (2, 54)):
                cells = {(x, y) for x in range(lanes) for y in range(5)}
                alpha = fixture_alpha(cells, (lanes, 5), density, masks=self.masks[density])
                for y in range(32 * density, 128 * density):
                    self.assertEqual(np.count_nonzero(alpha[y] == 255), expected_width * density)
                    self.assertTrue(np.all(alpha[y, 5 * density:(32 * lanes - 5) * density] == 255))
                self.assertTrue(np.all(alpha[:, :5 * density] == 0))
                self.assertTrue(np.all(alpha[:, -5 * density:] == 0))

    def test_all_rotated_l_t_cross_apron_and_loop_fixtures_keep_topology(self):
        fixtures = [
            {(1, 1), (1, 0), (2, 1)},
            {(1, 1), (1, 0), (0, 1), (2, 1)},
            {(1, 1), (1, 0), (0, 1), (2, 1), (1, 2)},
            {(x, y) for x in (0, 1) for y in (0, 1)},
            {(x, y) for x in range(3) for y in range(3) if (x, y) != (1, 1)},
            {(0, 0), (1, 1), (2, 2)},
            {(0, 0), (1, 0), (2, 2)},
        ]
        for density in (1, 2):
            for fixture in fixtures:
                cells = fixture
                for _ in range(4):
                    occupancy = np.zeros((3, 3), dtype=bool)
                    for x, y in cells:
                        occupancy[y, x] = True
                    alpha = fixture_alpha(cells, (3, 3), density, masks=self.masks[density])
                    self.assertEqual(components(alpha >= 2, diagonal=True), components(occupancy))
                    self.assertEqual(components(np.pad(alpha < 2, 1, constant_values=True)),
                                     components(np.pad(~occupancy, 1, constant_values=True)))
                    for y in range(3):
                        for x in range(3):
                            if (x, y) not in cells:
                                size = 32 * density
                                self.assertTrue(np.all(alpha[y * size:(y + 1) * size, x * size:(x + 1) * size] == 0))
                    cells = {(2 - y, x) for x, y in cells}

    def test_diagonal_only_neighbors_never_change_the_isolated_road_shape(self):
        for density in (1, 2):
            for diagonals in range(16):
                mask = sum(1 << (2 * bit + 1) for bit in range(4) if diagonals & (1 << bit))
                np.testing.assert_array_equal(self.masks[density][mask], self.masks[density][0])

    def test_actual_map_coverage_aprons_thresholds_and_open_board_entries(self):
        terrain = actual_terrain()
        self.assertEqual((len(terrain[0]), len(terrain)), (27, 17))
        cells = {(x, y) for y, row in enumerate(terrain) for x, tile in enumerate(row) if tile in '-qv'}
        self.assertEqual(len(cells), 84)
        virtual_ports = {(-1, 9), (-1, 10), (27, 6), (27, 7)}
        for density in (1, 2):
            size = 32 * density
            alpha = fixture_alpha(cells, (27, 17), density, cells | virtual_ports, self.masks[density])
            for y, row in enumerate(terrain):
                for x, tile in enumerate(row):
                    cell = alpha[y * size:(y + 1) * size, x * size:(x + 1) * size]
                    if tile not in '-qv':
                        self.assertTrue(np.all(cell == 0), (x, y, tile))
                    else:
                        self.assertEqual(cell[16 * density, 16 * density], 255)
            self.assertEqual(terrain[8][11:13], '..')
            for x, y in APRON_ANCHORS:
                self.assertTrue({(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)} <= cells)
                cx, cy = (x + 1) * size, (y + 1) * size
                self.assertTrue(np.all(alpha[cy - 5 * density:cy + 5 * density, cx - 5 * density:cx + 5 * density] == 255))
            for y in (9, 10):
                self.assertTrue(np.all(alpha[y * size + 5 * density:y * size + 27 * density, 0] == 255))
            for y in (6, 7):
                self.assertTrue(np.all(alpha[y * size + 5 * density:y * size + 27 * density, -1] == 255))
            self.assertEqual(components(alpha >= 2, diagonal=True), 1)

    def test_bridge_connects_land_core_without_painting_its_water_cell(self):
        cells = {(0, 1), (2, 1)}
        for density in (1, 2):
            size = 32 * density
            alpha = fixture_alpha(cells, (3, 3), density, cells | {(1, 1)}, self.masks[density])
            self.assertTrue(np.all(alpha[size:2 * size, size:2 * size] == 0))
            self.assertTrue(np.all(alpha[size + 5 * density:size + 27 * density, size - 1] == 255))
            self.assertTrue(np.all(alpha[size + 5 * density:size + 27 * density, 2 * size] == 255))

    def test_alpha_guard_rejects_road_diagonal_and_single_pixel_mutations(self):
        cells = {(0, 0), (1, 0), (0, 1), (1, 1)}
        alpha = fixture_alpha(cells, (3, 3))
        assert_exact_alpha(alpha, cells, (3, 3))
        changed = alpha.copy()
        changed[16, 16] -= 1
        with self.assertRaisesRegex(ValueError, 'alpha'):
            assert_exact_alpha(changed, cells, (3, 3))
        with self.assertRaisesRegex(ValueError, 'occupancy'):
            assert_exact_alpha(alpha, cells | {(2, 1)}, (3, 3))
        changed = alpha.copy()
        # Removing only the SE neighbor bit reintroduces the forbidden internal notch.
        mask = occupancy_mask((0, 0), cells) ^ (1 << 3)
        changed[:32, :32] = self.masks[1][mask]
        with self.assertRaisesRegex(ValueError, 'alpha'):
            assert_exact_alpha(changed, cells, (3, 3))


class RoadExportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.planned = plan(ROOT, ROOT / MANIFEST)

    def test_source_identity_all_pixels_and_independent_full_source_resampling(self):
        source_path = ROOT / PACK / SOURCE
        source_bytes = source_path.read_bytes()
        self.assertEqual(hashlib.sha256(source_bytes).hexdigest(), SOURCE_SHA256)
        with Image.open(source_path) as source:
            self.assertEqual(source.size, (1254, 1254))
            for _, output, report in self.planned[:2]:
                expected = source.convert('RGB').resize(output.size, Image.Resampling.LANCZOS)
                self.assertEqual(output.tobytes(), expected.tobytes())
                self.assertEqual(report['worldOrigin'], [0, 0])
                self.assertEqual(report['edgeBandPixels'], 0)
            # Low density is not derived from the high-density file.
            self.assertNotEqual(self.planned[0][1].tobytes(), self.planned[1][1].resize((512, 512), Image.Resampling.LANCZOS).tobytes())
        self.assertEqual(source_bytes, source_path.read_bytes())
        self.assertEqual([report['pixelsPerWorldUnit'] for _, _, report in self.planned[:2]], [[.5, .5], [1, 1]])

    def test_exported_material_and_every_white_alpha_mask_are_reproducible(self):
        self.assertEqual(export(ROOT, ROOT / MANIFEST, True), [report for _, _, report in self.planned])
        for _, atlas, report in self.planned[2:]:
            size = report['exportDensity'] * 32
            pixels = np.asarray(atlas)
            self.assertTrue(np.all(pixels[:, :, :3] == 255))
            for mask in range(256):
                x, y = mask % 16 * size, mask // 16 * size
                np.testing.assert_array_equal(pixels[y:y + size, x:x + size, 3], np.asarray(road_area_mask(size, mask)))

    def test_world_uv_and_rgb_mutations_fail_and_non_tile_chunks_match(self):
        material = np.asarray(self.planned[1][1])
        cells = {(x, y) for x in range(8) for y in range(5)}
        alpha = fixture_alpha(cells, (8, 5))
        rgb = sample_world_rgb(material, (256, 160))
        rgba = np.dstack((rgb, alpha))
        assert_opaque_rgb(rgba, material)
        for box in ((17, 13, 173, 141), (0, 0, 173, 160), (173, 0, 256, 160)):
            left, top, right, bottom = box
            chunk = sample_world_rgb(material, (right - left, bottom - top), (left, top))
            np.testing.assert_array_equal(chunk, rgba[top:bottom, left:right, :3])
            assert_opaque_rgb(rgba[top:bottom, left:right], material, (left, top))
        with self.assertRaisesRegex(ValueError, 'RGB'):
            assert_opaque_rgb(rgba, material, (1, 0))
        with self.assertRaisesRegex(ValueError, 'RGB'):
            assert_opaque_rgb(rgba[:, 173:], material, (0, 0))
        changed = rgba.copy()
        changed[64, 64, 0] ^= 1
        with self.assertRaisesRegex(ValueError, 'RGB'):
            assert_opaque_rgb(changed, material)
        self.assertFalse(np.array_equal(rgb[48:64, 48:64], rgb[48:64, 80:96]))

    def test_invalid_manifest_source_and_mask_mutations_fail_before_writes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = root / MANIFEST
            manifest.parent.mkdir(parents=True)
            source = root / PACK / SOURCE
            source.parent.mkdir(parents=True)
            shutil.copyfile(ROOT / PACK / SOURCE, source)
            environment = root / PACK / 'environment-builder-v1/manifest-environment-builder-v1.json'
            environment.parent.mkdir(parents=True)
            environment.write_text(json.dumps({'atlases': [{'id': 'route-dirt-road'}]}))
            data = json.loads((ROOT / MANIFEST).read_text())
            cases = [
                ({'sourceSha256': '0' * 64}, 'SHA-256'),
                ({'worldOrigin': [1, 0]}, 'zero origin'),
                ({'logicalSize': [512, 512]}, '1024-unit'),
                ({'fit': {'inset': 1, 'bleed': 0}}, 'uncropped'),
                ({'replaces': {'atlas': 'water-slow-river', 'mask': 'same'}}, 'route-dirt-road'),
                ({'occupancy': {**data['occupancy'], 'concaveRadius': 4}}, 'geometry'),
                ({'png': '../escape.png'}, 'escapes root'),
                ({'png2x': data['png']}, 'distinct'),
            ]
            for changes, error in cases:
                manifest.write_text(json.dumps({**data, **changes}))
                with self.subTest(changes=changes), self.assertRaisesRegex(ValueError, error):
                    export(root, manifest)
                self.assertFalse((manifest.parent / 'runtime').exists())
            manifest.write_text(json.dumps(data))
            export(root, manifest)
            target = manifest.parent / data['occupancy']['png2x']
            with Image.open(target) as image:
                changed = image.copy()
            changed.putpixel((32, 32), (255, 255, 255, 254))
            changed.save(target)
            with self.assertRaisesRegex(ValueError, 'stale road'):
                export(root, manifest, True)
            with Image.open(source) as image:
                changed = image.copy()
            pixel = changed.getpixel((500, 500))
            changed.putpixel((500, 500), (pixel[0] ^ 1, *pixel[1:]))
            changed.save(source)
            with self.assertRaisesRegex(ValueError, 'SHA-256'):
                export(root, manifest)


if __name__ == '__main__':
    unittest.main()
