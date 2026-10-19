import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from export_material_masks import BLOB_ORDER, PACK, ROOT, export


class ForestMaskTests(unittest.TestCase):
    atlas_id = 'transition-meadow-forest'
    material_name = 'forest'

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        relative = PACK / f'world-materials-v1/manifest-{self.material_name}-blend-masks-v1.json'
        self.manifest = self.root / relative
        self.manifest.parent.mkdir(parents=True)
        self.document = json.loads((ROOT / relative).read_text())
        self.environment = self.root / PACK / 'environment-builder-v1'
        self.environment.mkdir()
        self.catalog = self.environment / 'manifest-environment-builder-v1.json'
        actual = json.loads((ROOT / PACK / 'environment-builder-v1/manifest-environment-builder-v1.json').read_text())
        self.atlas = next(a for a in actual['atlases'] if a['id'] == self.atlas_id)
        for key in ['png', 'png2x']:
            source = ROOT / PACK / 'environment-builder-v1' / self.atlas[key]
            target = self.environment / self.atlas[key]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
        self.write()

    def write(self):
        self.catalog.write_text(json.dumps({'atlases': [self.atlas]}))
        self.manifest.write_text(json.dumps(self.document))

    def test_all_47_alpha_cells_and_empty_padding_preserved_at_both_densities(self):
        sources = {self.environment / self.atlas[k]: (self.environment / self.atlas[k]).read_bytes() for k in ['png', 'png2x']}
        catalog_before = self.catalog.read_bytes()
        reports = export(self.root, self.manifest)
        self.assertEqual(reports, export(self.root, self.manifest, check=True))
        self.assertEqual(len(reports), 2)
        self.assertEqual(self.atlas['maskOrder'], BLOB_ORDER)
        for report in reports:
            with Image.open(self.root / report['source']) as source, Image.open(self.root / report['output']) as output:
                self.assertEqual(output.mode, 'RGBA')
                self.assertEqual(output.getchannel('A').tobytes(), source.getchannel('A').tobytes())
                self.assertEqual(output.convert('RGB').getextrema(), ((255, 255),) * 3)
                self.assertEqual(len(report['cellAlphaSha256']), 47)
                cell = 32 * report['density']
                for index in range(48):
                    box = (index % 16 * cell, index // 16 * cell, (index % 16 + 1) * cell, (index // 16 + 1) * cell)
                    self.assertEqual(output.getchannel('A').crop(box).tobytes(), source.getchannel('A').crop(box).tobytes())
                    if index == 47:
                        self.assertEqual(output.getchannel('A').crop(box).getextrema(), (0, 0))
        self.assertEqual(sources, {p: p.read_bytes() for p in sources})
        self.assertEqual(catalog_before, self.catalog.read_bytes())

    def test_old_material_rgb_is_discarded_even_on_semitransparent_edges(self):
        before = [(self.root / r['output']).read_bytes() for r in export(self.root, self.manifest)]
        for key, hash_key in [('png', 'sourceSha256'), ('png2x', 'sourceSha2562x')]:
            path = self.environment / self.atlas[key]
            with Image.open(path) as original:
                changed = Image.new('RGBA', original.size, (183, 23, 210, 255))
                changed.putalpha(original.getchannel('A'))
            changed.save(path)
            self.document['atlases'][0][hash_key] = hashlib.sha256(path.read_bytes()).hexdigest()
        self.write()
        self.assertEqual(before, [(self.root / r['output']).read_bytes() for r in export(self.root, self.manifest)])

    def test_geometry_order_and_binding_must_match_the_actual_atlas(self):
        original = copy.deepcopy(self.atlas)
        for key, value in [('rows', 4), ('componentCount', 48), ('maskOrder', BLOB_ORDER[::-1]),
                           ('bitOrder', ['N', 'E', 'S', 'W']), ('overlaySurface', 'surface-snow')]:
            with self.subTest(key=key):
                self.atlas = {**original, key: value}
                self.write()
                with self.assertRaisesRegex(ValueError, 'changed'):
                    export(self.root, self.manifest)
        self.assertFalse((self.manifest.parent / 'runtime').exists())

    def test_hash_failure_and_unknown_atlas_reject_before_writing(self):
        self.document['atlases'][0]['sourceSha2562x'] = '0' * 64
        self.write()
        with self.assertRaisesRegex(ValueError, 'SHA-256'):
            export(self.root, self.manifest)
        self.assertFalse((self.manifest.parent / 'runtime').exists())
        self.document['atlases'][0]['id'] = 'transition-meadow-snow'
        self.write()
        with self.assertRaisesRegex(ValueError, 'reviewed'):
            export(self.root, self.manifest)

    def test_stale_check_does_not_repair_mask_or_native_source(self):
        report = export(self.root, self.manifest)[0]
        path = self.root / report['output']
        with Image.open(path) as image:
            image.putpixel((0, 0), (0, 0, 0, 0))
            image.save(path)
        before = path.read_bytes()
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.manifest, check=True)
        self.assertEqual(before, path.read_bytes())

    def test_an_invented_48th_cell_is_rejected_before_outputs(self):
        path = self.environment / self.atlas['png2x']
        with Image.open(path) as image:
            image.putpixel((15 * 64, 2 * 64), (255, 255, 255, 1))
            image.save(path)
        self.document['atlases'][0]['sourceSha2562x'] = hashlib.sha256(path.read_bytes()).hexdigest()
        self.write()
        with self.assertRaisesRegex(ValueError, 'padding slot'):
            export(self.root, self.manifest)
        self.assertFalse((self.manifest.parent / 'runtime').exists())


if __name__ == '__main__':
    unittest.main()
