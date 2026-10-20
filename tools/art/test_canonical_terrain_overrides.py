import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from export_canonical_terrain_overrides import (
    ALPHA_SHA256, PACK, ROOT, TOPICS, canonical_masks, edge_contract,
    export, pixel_hash, plan, review_previews,
)


class CanonicalTerrainTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.pack = self.root / PACK
        self.pack.mkdir(parents=True)
        self.catalog = self.root / 'catalog.json'
        self.base = self.pack / 'manifest-final-fantasy-v1.json'
        self.hd = self.pack / 'manifest-tactical-runtime-hd.json'
        for destination in (self.base, self.hd):
            actual = json.loads((ROOT / PACK / destination.name).read_text())
            actual['assets'] = [a for a in actual['assets'] if a['topicId'] in TOPICS]
            if destination == self.hd:
                actual['assetCount'] = len(actual['assets'])
            destination.write_text(json.dumps(actual))
        self.entries = {}
        for i, (topic, mode) in enumerate(TOPICS.items()):
            path = self.pack / 'masters/test-canonical' / f'{topic}.png'
            path.parent.mkdir(parents=True, exist_ok=True)
            image = Image.new('RGB', (192, 160))
            # Technical source fixtures expose recoloring, mistaken windows,
            # accidental density resampling and any interior seam edits.
            image.putdata([((x * 3 + i * 17) % 256, (y * 5 + i * 23) % 256, (x + y + i * 29) % 256)
                           for y in range(image.height) for x in range(image.width)])
            image.save(path)
            self.entries[topic] = {'source': str(path.relative_to(self.root)),
                                   'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                                   'registration': {'sourceSize': [192, 160], 'windowsXYXY':
                                                    [[0, 0, 96, 96], [96, 0, 192, 96], [0, 64, 96, 160], [96, 64, 192, 160]]
                                                    if mode == 'variants' else [[32, 16, 160, 144]]}}
        self.write_catalog()

    def write_catalog(self, entries=None):
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': self.entries if entries is None else entries}))

    def outputs_exist(self):
        return (self.pack / 'runtime').exists() or (self.pack / 'runtime-hd').exists()

    def fail_without_writes(self, pattern):
        self.write_catalog()
        with self.assertRaisesRegex(ValueError, pattern):
            export(self.root, self.catalog)
        self.assertFalse(self.outputs_exist())

    def test_registered_sources_export_both_densities_preserving_interiors_and_metadata(self):
        inputs = {p: p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        reports = export(self.root, self.catalog)
        self.assertEqual(reports, export(self.root, self.catalog, check=True))
        self.assertEqual(len(reports), len(TOPICS) * 2)
        self.assertEqual(sum(r['derivedCellCount'] for r in reports), sum(4 if mode == 'variants' else 16 for mode in TOPICS.values()) * 2)
        for path, original in inputs.items():
            self.assertEqual(path.read_bytes(), original)
        for report in reports:
            topic = report['topicId']
            entry = self.entries[topic]
            size = 32 * report['density']
            mode = TOPICS[topic]
            count = 4 if mode == 'variants' else 16
            self.assertEqual(report['size'], [size * count, size])
            self.assertEqual(report['authoredMaterialCount'], 1)
            self.assertEqual(report['environmentCellCountIncrement'], 0)
            self.assertIn('catalog-only', report['integration'])
            self.assertEqual(report['source']['registration'], entry['registration'])
            self.assertEqual(report['source']['sourceSha256'], entry['sha256'])
            self.assertEqual(report['edges']['pairs'], {'eastWest': 16, 'southNorth': 16} if mode == 'variants'
                             else {'eastWest': 64, 'southNorth': 64})
            with Image.open(self.root / report['output']) as sheet, Image.open(self.root / entry['source']) as source:
                self.assertEqual(sheet.mode, 'RGBA')
                self.assertEqual(pixel_hash(sheet.getchannel('A')), ALPHA_SHA256[mode, size])
                for index in range(count):
                    window = entry['registration']['windowsXYXY'][index if mode == 'variants' else 0]
                    sample = source.crop(window).resize((size, size), Image.Resampling.LANCZOS)
                    cell = sheet.crop((index * size, 0, (index + 1) * size, size))
                    for y in range(1, size - 1):
                        for x in range(1, size - 1):
                            actual = cell.getpixel((x, y))
                            if actual[3] == 255:
                                self.assertEqual(actual[:3], sample.getpixel((x, y)))
                            else:
                                self.assertEqual(actual, (0, 0, 0, 0))
                    self.assertEqual(report['cells'][index]['alphaSha256'], pixel_hash(cell.getchannel('A')))
                for sample in report['samples']:
                    self.assertEqual(sample['edgeBandPixels'], 1)
                    self.assertLessEqual(sample['changedPixels'], 4 * size - 4)

    def test_every_historical_alpha_mask_and_cardinal_edge_span(self):
        for size, directory in ((32, 'runtime'), (64, 'runtime-hd')):
            masks = canonical_masks(size)
            alpha_strip = Image.new('L', (16 * size, size))
            for bits, alpha in enumerate(masks):
                alpha_strip.paste(alpha, (bits * size, 0))
                self.assertEqual(set(alpha.tobytes()), {0, 255})
                self.assertEqual(alpha.getpixel((size // 2, size // 2)), 255)
                for bit, box in ((1, (0, 0, size, 1)), (2, (size - 1, 0, size, size)),
                                 (4, (0, size - 1, size, size)), (8, (0, 0, 1, size))):
                    expected = [255 if bits & bit and 3 * size // 8 <= i < 5 * size // 8 else 0 for i in range(size)]
                    self.assertEqual(list(alpha.crop(box).tobytes()), expected)
            self.assertEqual(pixel_hash(alpha_strip), ALPHA_SHA256['nesw-16', size])
            self.assertEqual(alpha_strip.histogram()[255], 4608 if size == 32 else 18496)
            for topic in ('terrain-border-2', 'terrain-border-4', 'terrain-capital-1', 'terrain-capital-2'):
                with Image.open(ROOT / PACK / directory / 'terrain' / f'{topic}.png') as historical:
                    self.assertEqual(alpha_strip.tobytes(), historical.getchannel('A').tobytes())
        nearest = canonical_masks(32)[0].resize((64, 64), Image.Resampling.NEAREST)
        self.assertNotEqual(nearest.tobytes(), canonical_masks(64)[0].tobytes())

    def test_all_allowed_neighbors_are_compared_and_failure_is_detected(self):
        for _, sheet, report in plan(self.root, self.catalog):
            size = sheet.height
            cells = [sheet.crop((i * size, 0, (i + 1) * size, size)) for i in range(sheet.width // size)]
            mode = report['tileMode']
            # Mutate a late-index open edge so a first-cell-only check would miss it.
            cell = cells[-1]
            x, y = size - 1, size // 2
            r, g, b, a = cell.getpixel((x, y))
            cell.putpixel((x, y), ((r + 1) % 256, g, b, a))
            with self.assertRaisesRegex(ValueError, 'edge mismatch'):
                edge_contract(cells, mode)

    def test_previews_copy_exact_cells_and_connect_each_neighbor(self):
        for _, sheet, report in plan(self.root, self.catalog):
            mode, size = report['tileMode'], sheet.height
            previews = review_previews(sheet, mode)
            if mode == 'variants':
                self.assertEqual(set(previews), {'repeated-2x2', 'mixed-4x4'})
                for y in range(4):
                    for x in range(4):
                        expected = sheet.crop(((x + y) % 4 * size, 0, ((x + y) % 4 + 1) * size, size)).convert('RGB')
                        self.assertEqual(previews['mixed-4x4'].crop((x * size, y * size, (x + 1) * size, (y + 1) * size)).tobytes(), expected.tobytes())
            else:
                for name, bits in (('isolated', 0), ('end', 1), ('straight-ns', 5), ('straight-ew', 10),
                                   ('corner', 3), ('tee', 7), ('cross', 15)):
                    expected = sheet.crop((bits * size, 0, (bits + 1) * size, size))
                    self.assertEqual(previews[name].crop((size, size, 2 * size, 2 * size)).tobytes(), expected.tobytes())
                    for bit, neighbor, x, y in ((1, 4, 1, 0), (2, 8, 2, 1), (4, 1, 1, 2), (8, 2, 0, 1)):
                        actual = previews[name].crop((x * size, y * size, (x + 1) * size, (y + 1) * size))
                        expected = sheet.crop((neighbor * size, 0, (neighbor + 1) * size, size)) if bits & bit else Image.new('RGBA', (size, size))
                        self.assertEqual(actual.tobytes(), expected.tobytes())

    def test_empty_catalog_is_noop_without_even_manifests(self):
        self.write_catalog({})
        self.base.unlink()
        self.hd.unlink()
        self.assertEqual(export(self.root, self.catalog), [])
        self.assertEqual(export(self.root, self.catalog, check=True), [])
        self.assertFalse(self.outputs_exist())

    def test_single_topic_leaves_unregistered_runtime_and_manifests_untouched(self):
        untouched = self.pack / 'runtime/terrain/terrain-capital-1.png'
        untouched.parent.mkdir(parents=True)
        untouched.write_bytes(b'unregistered terrain sentinel')
        self.write_catalog({'C01-TERRAIN-BORDER-2': self.entries['C01-TERRAIN-BORDER-2']})
        before = self.base.read_bytes(), self.hd.read_bytes()
        self.assertEqual(len(export(self.root, self.catalog)), 2)
        self.assertEqual((self.base.read_bytes(), self.hd.read_bytes()), before)
        self.assertEqual(untouched.read_bytes(), b'unregistered terrain sentinel')
        self.assertFalse((self.pack / 'runtime/terrain/terrain-border-1.png').exists())

    def test_bad_later_source_hash_is_rejected_before_any_write(self):
        self.entries['C01-TERRAIN-BORDER-4']['sha256'] = '0' * 64
        self.fail_without_writes('SHA-256 mismatch')

    def test_bad_sources_and_paths_rejected_before_writes(self):
        entry = self.entries['C01-TERRAIN-BORDER-4']
        original = copy.deepcopy(entry)
        for value in ('../outside.png', '/tmp/outside.png', 'packages/story-candidate-02/assets/master.png',
                      str(PACK / 'runtime/terrain/terrain-border-4.png')):
            entry['source'] = value
            self.fail_without_writes('relative path|escapes root|own pack masters')
        entry.update(original)
        entry['sha256'] = 'INVALID'
        self.fail_without_writes('64 lowercase hex')

    def test_nonopaque_nonpng_and_small_sources_rejected_before_writes(self):
        entry = self.entries['C01-TERRAIN-BORDER-4']
        path = self.root / entry['source']
        for image, fmt, message in ((Image.new('RGBA', (192, 160), (10, 20, 30, 254)), 'PNG', 'fully opaque'),
                                    (Image.new('RGB', (192, 160)), 'JPEG', 'PNG material'),
                                    (Image.new('RGB', (32, 32)), 'PNG', 'at least 64px')):
            image.save(path, format=fmt)
            entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
            self.fail_without_writes(message)

    def test_invalid_registration_rejected_before_writes(self):
        entry = self.entries['C01-TERRAIN-BORDER-3']
        original = copy.deepcopy(entry['registration'])
        changes = [({'sourceSize': [160, 192]}, 'sourceSize'),
                   ({'sourceSize': [192.0, 160]}, 'sourceSize'),
                   ({'windowsXYXY': [[0, 0, 64, 64]]}, '4 explicit'),
                   ({'windowsXYXY': [[0, 0, 64, 64]] * 4}, 'distinct'),
                   ({'windowsXYXY': [[0, 0, 96, 95]] * 4}, 'integer squares'),
                   ({'windowsXYXY': [[0, 0, 63, 63]] * 4}, 'at least 64px'),
                   ({'windowsXYXY': [[True, 0, 96, 96]] * 4}, 'integer squares'),
                   ({'windowsXYXY': [[-1, 0, 95, 96]] * 4}, 'in-bounds'),
                   ({'windowsXYXY': [[0, 0, 96, 96], [96, 0, 192, 96], [0, 64, 96, 160], [0, 0, 128, 128]]}, 'common material scale'),
                   ({'rotation': 90}, 'registration requires')]
        for change, message in changes:
            with self.subTest(change=change):
                entry['registration'] = {**original, **change}
                self.fail_without_writes(message)

    def test_unknown_topics_duplicate_keys_and_shared_source_rejected(self):
        self.entries['C01-TERRAIN-FORGE-1'] = self.entries.pop('C01-TERRAIN-BORDER-1')
        self.fail_without_writes('unsupported canonical')
        self.entries['C01-TERRAIN-BORDER-1'] = self.entries.pop('C01-TERRAIN-FORGE-1')
        self.entries['C01-TERRAIN-BORDER-3'] = self.entries['C01-TERRAIN-BORDER-1']
        self.fail_without_writes('own authored material')
        self.catalog.write_text('{"schemaVersion":1,"assets":{},"assets":{}}')
        with self.assertRaisesRegex(ValueError, 'duplicate JSON key'):
            export(self.root, self.catalog)
        self.assertFalse(self.outputs_exist())

    def test_manifest_contract_changes_rejected_before_writes(self):
        for path, key, value in ((self.base, 'tileWidth', 64), (self.base, 'anchor', [16, 16]),
                                 (self.base, 'variantOrder', ['d', 'c', 'b', 'a']),
                                 (self.base, 'png', '../outside.png'), (self.hd, 'pixelDensity', 1),
                                 (self.hd, 'width', 128)):
            original = path.read_bytes()
            data = json.loads(original)
            data['assets'][0][key] = value
            path.write_text(json.dumps(data))
            with self.subTest(path=path, key=key):
                self.fail_without_writes('canonical.*geometry')
            path.write_bytes(original)
        data = json.loads(self.hd.read_text())
        data['assets'].append(data['assets'][0])
        data['assetCount'] += 1
        self.hd.write_text(json.dumps(data))
        self.fail_without_writes('exactly one')

    def test_destination_symlinks_rejected_before_writes(self):
        destination = self.pack / 'runtime'
        destination.symlink_to(self.pack / 'masters', target_is_directory=True)
        self.write_catalog()
        with self.assertRaisesRegex(ValueError, 'must not use symlinks'):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'masters/terrain').exists())

    def test_check_is_read_only_and_detects_stale_export(self):
        report = export(self.root, self.catalog)[-1]
        path = self.root / report['output']
        with Image.open(path) as opened:
            image = opened.copy()
        image.putpixel((32, 32), (0, 0, 0, 255))
        image.save(path)
        before = {p: p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        with self.assertRaisesRegex(ValueError, 'stale canonical terrain'):
            export(self.root, self.catalog, check=True)
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.rglob('*') if p.is_file()})


if __name__ == '__main__':
    unittest.main()
