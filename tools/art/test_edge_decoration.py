import copy
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from PIL import Image
from export_overrides import ROOT
from export_edge_decoration import MANIFEST, PACK, export


class EdgeDecorationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.manifest = self.root / MANIFEST
        self.data = json.loads((ROOT / MANIFEST).read_text())
        files = [MANIFEST, *(PACK / a['source'] for a in self.data['assets']),
                 *(Path(d['source']) for d in self.data['geometryDependencies'])]
        for relative in files:
            target = self.root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, target)

    def write_manifest(self):
        self.manifest.write_text(json.dumps(self.data))

    def test_production_source_graph_and_reproducibility(self):
        reports = export(ROOT, ROOT / MANIFEST, True)
        self.assertEqual(len(reports), 4)
        self.assertTrue(all(r['alpha']['edgeMaxAlpha'] <= 1 for r in reports))

    def test_every_rgba_pixel_is_exact_full_canvas_resize_with_transparent_margin(self):
        sources = {self.root / PACK / a['source']: (self.root / PACK / a['source']).read_bytes()
                   for a in self.data['assets']}
        reports = export(self.root, self.manifest)
        for asset in self.data['assets']:
            with Image.open(self.root / PACK / asset['source']) as source:
                padding = asset['sourcePadding']
                padded = Image.new('RGBA', tuple(n + padding * 2 for n in source.size))
                padded.paste(source, (padding, padding))
                self.assertEqual(padded.crop((padding, padding, padding + source.width, padding + source.height)).tobytes(), source.tobytes())
                for report in (r for r in reports if r['id'] == asset['id']):
                    expected = padded.resize(tuple(report['rasterSize']), Image.Resampling.LANCZOS)
                    with Image.open(self.root / report['output']) as actual:
                        self.assertEqual(actual.mode, 'RGBA')
                        self.assertEqual(actual.tobytes(), expected.tobytes())
                        self.assertEqual(hashlib.sha256(actual.getchannel('A').tobytes()).hexdigest(), report['alphaSha256'])
                    self.assertLessEqual(report['alpha']['edgeMaxAlpha'], 1)
        self.assertEqual({p: p.read_bytes() for p in sources}, sources)
        export(self.root, self.manifest, True)

    def test_invalid_later_source_or_geometry_never_writes_any_output(self):
        original = copy.deepcopy(self.data)
        for change in [{'sourceSha256': 'bad'}, {'sourceSize': [1, 1]},
                       {'sourceAnchor': [-1, 0]}, {'logicalPerSourcePixel': 2},
                       {'sourcePadding': -1}, {'meaningfulSourceBounds': [0, 0, 1, 1]},
                       {'png': 'runtime/other.png'}]:
            with self.subTest(change=change):
                self.data = copy.deepcopy(original)
                self.data['assets'][-1].update(change)
                self.write_manifest()
                with self.assertRaises(ValueError):
                    export(self.root, self.manifest)
                self.assertFalse((self.manifest.parent / 'runtime').exists())

    def test_changed_road_or_water_geometry_requires_layout_review(self):
        for dependency in self.data['geometryDependencies']:
            with self.subTest(source=dependency['source']):
                path = self.root / dependency['source']
                before = path.read_bytes()
                path.write_bytes(before + b'\n')
                with self.assertRaisesRegex(ValueError, 'geometry dependency changed'):
                    export(self.root, self.manifest)
                path.write_bytes(before)
                self.assertFalse((self.manifest.parent / 'runtime').exists())

    def test_stale_output_and_invalid_source_alpha_are_rejected(self):
        reports = export(self.root, self.manifest)
        target = self.root / reports[0]['output']
        target.write_bytes(b'bad')
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.manifest, True)
        asset = self.data['assets'][0]
        path = self.root / PACK / asset['source']
        with Image.open(path) as source:
            changed = source.copy()
        changed.putpixel((0, 0), (20, 30, 40, 2))
        changed.save(path)
        asset['sourceSha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, 'outer edge alpha'):
            export(self.root, self.manifest)
        self.assertEqual(target.read_bytes(), b'bad')


if __name__ == '__main__':
    unittest.main()
