import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_overrides import PACKS, export, fit, read_json, validate_alpha


class ExportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.catalog = self.root / 'catalog.json'
        self.pack = None
        for i, (campaign, directory, suffix) in enumerate(PACKS):
            pack = self.root / 'packages' / campaign / 'assets' / directory
            pack.mkdir(parents=True)
            assets = []
            if i == 0:
                self.pack = pack
                assets = [{'topicId': 'C01-PROP-01', 'category': 'battle-prop', 'width': 64, 'height': 64,
                           'anchor': [32, 32], 'png': 'runtime/battle-prop/prop-01.png'}]
                (pack / 'manifest-tactical-runtime-hd.json').write_text(json.dumps({'schemaVersion': 'candidate-01-runtime-hd-v2', 'assetCount': 1, 'assets': [
                    {'topicId': 'C01-PROP-01', 'width': 128, 'height': 128, 'pixelDensity': 2,
                     'png': 'runtime-hd/battle-prop/prop-01.png'}]}))
            (pack / f'manifest-final-{suffix}-v1.json').write_text(json.dumps({'assets': assets}))
        self.source = self.pack / 'masters/remake/prop-01.png'
        self.source.parent.mkdir(parents=True)
        image = Image.new('RGBA', (60, 60), (0, 0, 0, 0))
        image.paste((80, 90, 100, 253), (10, 10, 50, 50))
        image.putpixel((9, 10), (80, 90, 100, 100))
        image.save(self.source)
        self.register()

    def register(self):
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': {'C01-PROP-01': {
            'source': str(self.source.relative_to(self.root)),
            'sha256': hashlib.sha256(self.source.read_bytes()).hexdigest()}}}))

    def test_export_both_densities_and_check(self):
        unrelated = self.pack / 'untouched.png'
        unrelated.write_bytes(b'legacy unchanged')
        before = {p: p.read_bytes() for p in self.root.rglob('*.json')}
        result = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in result], [[64, 64], [128, 128]])
        export(self.root, self.catalog, True)
        self.assertEqual(unrelated.read_bytes(), b'legacy unchanged')
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.rglob('*.json')})

    def test_registered_battle_prop_anchor_is_metadata_not_pixel_transform(self):
        manifest = self.pack / 'manifest-final-fantasy-v1.json'
        data = json.loads(manifest.read_text())
        original = export(self.root, self.catalog)
        before = [(self.root / r['output']).read_bytes() for r in original]
        data['assets'][0]['anchor'] = [32, 58.5]
        manifest.write_text(json.dumps(data))
        after = export(self.root, self.catalog)
        self.assertEqual(before, [(self.root / r['output']).read_bytes() for r in after])
        data['assets'][0]['anchor'] = [32, 65]
        manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'anchor'):
            export(self.root, self.catalog)

    def test_tactical_prop_rejects_unintended_density_change(self):
        path = self.pack / 'manifest-tactical-runtime-hd.json'
        manifest = json.loads(path.read_text())
        manifest['assets'][0].update(width=416, height=416, pixelDensity=6.5)
        path.write_text(json.dumps(manifest))
        with self.assertRaisesRegex(ValueError, 'unsupported HD contract'):
            export(self.root, self.catalog)

    def test_fit_preserves_authored_alpha_without_double_mask(self):
        with Image.open(self.source) as source:
            result = fit(source, (64, 64), 2)
        self.assertEqual(result.getpixel((11, 12)), (80, 90, 100, 100))
        self.assertEqual(result.getpixel((12, 12)), (80, 90, 100, 253))

    def test_opaque_and_empty_rejected(self):
        for alpha in (0, 255):
            with self.assertRaises(ValueError):
                validate_alpha(Image.new('RGBA', (4, 4), (20, 30, 40, alpha)), 'test')

    def test_one_step_edge_residue_accepted_and_reported_without_mutation(self):
        with Image.open(self.source) as image:
            image.putpixel((0, 20), (100, 120, 140, 1))
            image.save(self.source)
        self.register()
        before = self.source.read_bytes()
        result = export(self.root, self.catalog)
        self.assertEqual(result[0]['sourceAlpha'], {
            'edgeMaxAlpha': 1, 'edgeNonzeroPixels': 1,
            'coreBoundingBox': [10, 10, 50, 50]})
        self.assertEqual(self.source.read_bytes(), before)
        with Image.open(self.root / result[0]['output']) as rendered:
            self.assertEqual(rendered.getpixel((2, 22)), (100, 120, 140, 1))

    def test_edge_contact_rejected(self):
        for edge_alpha in (2, 8, 28, 240, 255):
            with self.subTest(edge_alpha=edge_alpha), Image.open(self.source) as image:
                image.putpixel((0, 20), (0, 0, 0, edge_alpha))
                with self.assertRaises(ValueError):
                    validate_alpha(image, 'test')

    def test_hash_mismatch_rejected_before_writes(self):
        self.source.write_bytes(self.source.read_bytes() + b'changed')
        with self.assertRaisesRegex(ValueError, 'SHA-256'):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())

    def test_unknown_topic_rejected(self):
        data = json.loads(self.catalog.read_text())
        data['assets']['unknown'] = data['assets'].pop('C01-PROP-01')
        self.catalog.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'unknown topic'):
            export(self.root, self.catalog)

    def test_duplicate_keys_rejected(self):
        self.catalog.write_text('{"assets": {}, "assets": {}}')
        with self.assertRaisesRegex(ValueError, 'duplicate'):
            read_json(self.catalog)

    def test_source_escape_rejected(self):
        data = json.loads(self.catalog.read_text())
        data['assets']['C01-PROP-01']['source'] = '../outside.png'
        self.catalog.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'escapes'):
            export(self.root, self.catalog)

    def test_stale_export_rejected(self):
        output = export(self.root, self.catalog)[0]['output']
        Image.new('RGBA', (64, 64)).save(self.root / output)
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.catalog, True)

    def test_later_invalid_entry_prevents_all_writes(self):
        data = json.loads(self.catalog.read_text())
        data['assets']['unknown'] = data['assets']['C01-PROP-01']
        self.catalog.write_text(json.dumps(data))
        with self.assertRaises(ValueError):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())

    def test_unsupported_category_rejected(self):
        manifest = self.pack / 'manifest-final-fantasy-v1.json'
        data = json.loads(manifest.read_text())
        data['assets'][0]['category'] = 'combat-unit'
        manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'only static'):
            export(self.root, self.catalog)

    def test_static_icon_contracts(self):
        manifest = self.pack / 'manifest-final-fantasy-v1.json'
        data = json.loads(manifest.read_text())
        (self.pack / 'manifest-tactical-runtime-hd.json').unlink()
        for category, side in [('equipment', 48), ('skill', 48), ('status', 32), ('hud', 32)]:
            with self.subTest(category=category):
                data['assets'][0].update(category=category, width=side, height=side,
                                         anchor=[side // 2, side // 2], png=f'runtime/{category}/icon.png')
                manifest.write_text(json.dumps(data))
                result = export(self.root, self.catalog)
                self.assertEqual([r['size'] for r in result], [[side, side]])
                export(self.root, self.catalog, True)
                data['assets'][0]['anchor'] = [0, 0]
                manifest.write_text(json.dumps(data))
                with self.assertRaisesRegex(ValueError, 'size/anchor'):
                    export(self.root, self.catalog)

    def test_empty_catalog_no_changes(self):
        self.catalog.write_text('{"schemaVersion": 1, "assets": {}}')
        self.assertEqual(export(self.root, self.catalog), [])
        self.assertFalse((self.pack / 'runtime').exists())


if __name__ == '__main__':
    unittest.main()
