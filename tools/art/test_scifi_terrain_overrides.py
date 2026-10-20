import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from export_canonical_terrain_overrides import (
    ALPHA_SHA256, ROOT, SCIFI_PACK, SCIFI_TOPICS, export, pixel_hash,
)


class ScifiTerrainExportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.pack = self.root / SCIFI_PACK
        self.pack.mkdir(parents=True)
        self.manifest = self.pack / 'manifest-final-scifi-v1.json'
        document = json.loads((ROOT / SCIFI_PACK / self.manifest.name).read_text())
        document['assets'] = [a for a in document['assets'] if a['topicId'] in SCIFI_TOPICS]
        self.manifest.write_text(json.dumps(document))
        self.entries = {}
        for index, (topic, mode) in enumerate(SCIFI_TOPICS.items()):
            source = self.pack / 'masters' / f'{topic}.png'
            source.parent.mkdir(exist_ok=True)
            image = Image.new('RGB', (128, 128))
            image.putdata([((x + index * 23) % 256, y, (x + y) % 256)
                           for y in range(128) for x in range(128)])
            image.save(source)
            self.entries[topic] = {
                'source': str(source.relative_to(self.root)),
                'sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                'registration': {'sourceSize': [128, 128], 'windowsXYXY':
                                 [[0, 0, 64, 64], [64, 0, 128, 64],
                                  [0, 64, 64, 128], [64, 64, 128, 128]]
                                 if mode == 'variants' else [[0, 0, 128, 128]]},
            }
        self.catalog = self.root / 'catalog.json'

    def run_export(self, check=False):
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': self.entries}))
        return export(self.root, self.catalog, check)

    def test_scifi_exports_only_declared_native_density_without_fantasy_or_hd_files(self):
        reports = self.run_export()
        self.assertEqual(len(reports), len(SCIFI_TOPICS))
        self.assertEqual(reports, self.run_export(check=True))
        self.assertFalse((self.pack / 'runtime-hd').exists())
        for report in reports:
            self.assertEqual(report['density'], 1)
            self.assertEqual(report['size'][1], 32)
            self.assertEqual(report['alphaSha256'], ALPHA_SHA256[report['tileMode'], 32])

    def test_original_scifi_alpha_matches_each_reviewed_historical_mask_contract(self):
        original = json.loads((ROOT / SCIFI_PACK / self.manifest.name).read_text())
        for asset in original['assets']:
            if asset['topicId'] in SCIFI_TOPICS:
                with Image.open(ROOT / SCIFI_PACK / asset['png']) as image:
                    self.assertEqual(pixel_hash(image.getchannel('A')),
                                     ALPHA_SHA256[SCIFI_TOPICS[asset['topicId']], 32])

    def test_wrong_campaign_source_rejected_before_any_output(self):
        entry = next(iter(self.entries.values()))
        other = self.root / 'packages/story-candidate-01/assets/final-fantasy-v1/masters/foreign.png'
        other.parent.mkdir(parents=True)
        other.write_bytes((self.root / entry['source']).read_bytes())
        entry['source'] = str(other.relative_to(self.root))
        with self.assertRaisesRegex(ValueError, 'own pack masters'):
            self.run_export()
        self.assertFalse((self.pack / 'runtime').exists())

    def test_changed_geometry_and_unexpected_density_rejected(self):
        original = self.manifest.read_text()
        for field, value in [('width', 256), ('pixelDensity', 2), ('anchor', [16, 16])]:
            document = json.loads(original)
            document['assets'][0][field] = value
            self.manifest.write_text(json.dumps(document))
            with self.assertRaisesRegex(ValueError, 'geometry or metadata'):
                self.run_export()
            self.assertFalse((self.pack / 'runtime').exists())

    def test_duplicate_canonical_identity_rejected(self):
        document = json.loads(self.manifest.read_text())
        document['assets'].append(document['assets'][0])
        self.manifest.write_text(json.dumps(document))
        with self.assertRaisesRegex(ValueError, 'exactly one'):
            self.run_export()

    def test_variant_windows_cannot_repeat_one_material_sample(self):
        entry = next(iter(self.entries.values()))
        entry['registration']['windowsXYXY'] = [[0, 0, 64, 64]] * 4
        with self.assertRaisesRegex(ValueError, 'distinct source windows'):
            self.run_export()
