import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from export_stone_masks import ATLAS_IDS, MANIFEST, PACK, ROOT, export


class StoneMaskTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.manifest = self.root / MANIFEST
        self.manifest.parent.mkdir(parents=True)
        self.document = json.loads((ROOT / MANIFEST).read_text())
        self.environment = self.root / PACK / 'environment-builder-v1'
        self.environment.mkdir()
        self.catalog = self.environment / 'manifest-environment-builder-v1.json'
        actual = json.loads((ROOT / PACK / 'environment-builder-v1/manifest-environment-builder-v1.json').read_text())
        self.atlases = [a for a in actual['atlases'] if a['id'] in ATLAS_IDS]
        self.catalog.write_text(json.dumps({'atlases': self.atlases}))
        for atlas in self.atlases:
            for key in ['png', 'png2x']:
                source = ROOT / PACK / 'environment-builder-v1' / atlas[key]
                target = self.environment / atlas[key]
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(source.read_bytes())
        self.write()

    def write(self):
        self.manifest.write_text(json.dumps(self.document))

    def test_all_128_connector_cells_preserve_each_alpha_byte_at_both_densities(self):
        sources = {self.environment / a[k]: (self.environment / a[k]).read_bytes()
                   for a in self.atlases for k in ['png', 'png2x']}
        manifests = {p: p.read_bytes() for p in [self.catalog, self.manifest]}
        reports = export(self.root, self.manifest)
        self.assertEqual(len(reports), 4)
        for report in reports:
            with Image.open(self.root / report['source']) as source, Image.open(self.root / report['output']) as output:
                self.assertEqual(output.mode, 'RGBA')
                self.assertEqual(output.size, source.size)
                self.assertEqual(output.getchannel('A').tobytes(), source.getchannel('A').tobytes())
                self.assertEqual(output.convert('RGB').getextrema(), ((255, 255),) * 3)
                self.assertEqual(report['alphaRange'], list(source.getchannel('A').getextrema()))
                self.assertEqual(len(report['cellAlphaSha256']), 64)
                cell = 32 * report['density']
                for index in range(64):
                    box = (index % 16 * cell, index // 16 * cell, (index % 16 + 1) * cell, (index // 16 + 1) * cell)
                    self.assertEqual(output.getchannel('A').crop(box).tobytes(), source.getchannel('A').crop(box).tobytes())
        self.assertEqual(export(self.root, self.manifest, check=True), reports)
        self.assertEqual(sources, {p: p.read_bytes() for p in sources})
        self.assertEqual(manifests, {p: p.read_bytes() for p in manifests})

    def test_old_rgb_cannot_leak_back_into_the_technical_masks(self):
        before = [(self.root / r['output']).read_bytes() for r in export(self.root, self.manifest)]
        for atlas, spec in zip(self.atlases, self.document['atlases']):
            for key, hash_key in [('png', 'sourceSha256'), ('png2x', 'sourceSha2562x')]:
                path = self.environment / atlas[key]
                with Image.open(path) as current:
                    changed = Image.new('RGBA', current.size, (19, 82, 130, 255))
                    changed.putalpha(current.getchannel('A'))
                changed.save(path)
                spec[hash_key] = hashlib.sha256(path.read_bytes()).hexdigest()
        self.write()
        after = [(self.root / r['output']).read_bytes() for r in export(self.root, self.manifest)]
        self.assertEqual(before, after)

    def test_later_source_hash_failure_prevents_every_output_write(self):
        self.document['atlases'][1]['sourceSha2562x'] = '0' * 64
        self.write()
        with self.assertRaisesRegex(ValueError, 'SHA-256'):
            export(self.root, self.manifest)
        self.assertFalse((self.manifest.parent / 'runtime').exists())

    def test_indices_geometry_and_layer_order_are_not_reinterpreted(self):
        baseline = copy.deepcopy(self.atlases)
        for key, value in [('columns', 8), ('bitOrder', ['W', 'S', 'E', 'N']),
                           ('variantsPerMask', 3), ('cellIndex', 'variant * 16 + mask')]:
            with self.subTest(key=key):
                self.atlases = copy.deepcopy(baseline)
                self.atlases[0][key] = value
                self.catalog.write_text(json.dumps({'atlases': self.atlases}))
                with self.assertRaisesRegex(ValueError, 'changed'):
                    export(self.root, self.manifest)
        self.document['atlases'].reverse()
        self.write()
        with self.assertRaisesRegex(ValueError, 'layer order'):
            export(self.root, self.manifest)

    def test_duplicate_target_and_path_escape_rejected(self):
        self.document['atlases'][1]['png'] = self.document['atlases'][0]['png']
        self.write()
        with self.assertRaisesRegex(ValueError, 'distinct'):
            export(self.root, self.manifest)
        self.document['atlases'][1]['png'] = '../escape.png'
        self.write()
        with self.assertRaisesRegex(ValueError, 'path escapes root'):
            export(self.root, self.manifest)

    def test_stale_check_does_not_repair_output(self):
        report = export(self.root, self.manifest)[0]
        output = self.root / report['output']
        with Image.open(output) as im:
            im.putpixel((0, 0), (0, 0, 0, 0))
            im.save(output)
        before = output.read_bytes()
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.manifest, check=True)
        self.assertEqual(output.read_bytes(), before)


if __name__ == '__main__':
    unittest.main()
