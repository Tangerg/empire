import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_environment_overrides import PACK, export, cell_box, render_cell


class EnvironmentTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.pack = self.root / PACK
        self.env = self.pack / 'environment-builder-v1'
        self.env.mkdir(parents=True)
        order = [f'cell-{i}' for i in range(16)]
        self.manifest = self.env / 'manifest-environment-builder-v1.json'
        self.manifest.write_text(json.dumps({'atlases': [{
            'id': 'forest-temperate', 'columns': 4, 'rows': 4, 'cellWidth': 96,
            'cellHeight': 96, 'cellWidth2x': 192, 'cellHeight2x': 192, 'anchor': [48, 91],
            'tileMode': 'modules', 'cellOrder': order,
            'cells': [{'id': c, 'anchor': [48, 91], 'footprint': [1, 1]} for c in order],
            'png': 'runtime/atlas/forest-temperate.png', 'png2x': 'runtime/atlas/forest-temperate@2x.png'}]}))
        baselines = {}
        for density, size in ((1, 96), (2, 192)):
            image = Image.new('RGBA', (size * 4, size * 4))
            for i in range(16):
                image.paste((i * 10, 30, 60, 200), cell_box(i, size))
            baselines[f'{density}x'] = self.save(image, f'base-{density}.png')
        source = Image.new('RGBA', (172, 172))
        source.paste((45, 100, 60, 253), (10, 10, 162, 162))
        source.putpixel((0, 20), (30, 50, 70, 1))
        entry = self.save(source, 'tree.png')
        self.data = {'schemaVersion': 1, 'atlases': {'forest-temperate': {
            'baselines': baselines, 'cells': {'cell-0': entry, 'cell-2': entry}}}}
        self.catalog = self.root / 'catalog.json'
        self.write_catalog()

    def save(self, image, name):
        path = self.pack / 'masters/remake' / name
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path)
        return {'source': str(path.relative_to(self.root)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

    def write_catalog(self):
        self.catalog.write_text(json.dumps(self.data))

    def test_two_cells_preserve_fourteen_and_manifest(self):
        before = self.manifest.read_bytes()
        report = export(self.root, self.catalog)
        self.assertEqual([r['density'] for r in report], [1, 2])
        for record in report:
            for cell in record['cells']:
                if cell['index'] not in (0, 2):
                    self.assertEqual(cell['baselinePixelSha256'], cell['outputPixelSha256'])
                else:
                    self.assertNotEqual(cell['baselinePixelSha256'], cell['outputPixelSha256'])
        export(self.root, self.catalog, True)
        self.assertEqual(before, self.manifest.read_bytes())

    def test_complete_canvas_alpha_and_bottom_placement(self):
        source = Image.new('RGBA', (172, 172))
        source.paste((20, 30, 40, 253), (10, 10, 160, 160))
        source.putpixel((0, 20), (30, 50, 70, 1))
        original = source.tobytes()
        small, hd = render_cell(source)
        self.assertEqual(hd.getpixel((10, 30)), (30, 50, 70, 1))
        self.assertEqual(hd.getpixel((20, 20)), (20, 30, 40, 253))
        self.assertEqual(source.tobytes(), original)
        self.assertEqual(small.size, (96, 96))

    def test_explicit_source_placement_preserves_alpha(self):
        entry = self.data['atlases']['forest-temperate']['cells']['cell-0']
        entry['offsetY2x'] = 8
        self.write_catalog()
        reports = export(self.root, self.catalog)
        self.assertEqual(reports[0]['sources']['cell-0']['offsetY2x'], 8)
        with Image.open(self.root / entry['source']) as source:
            _, normal = render_cell(source)
            _, moved = render_cell(source, 8)
            self.assertEqual(normal.crop((10, 10, 182, 182)).tobytes(),
                             moved.crop((10, 18, 182, 190)).tobytes())
            for invalid in (-11, 11, 1.5, True):
                with self.assertRaisesRegex(ValueError, 'offsetY2x'):
                    render_cell(source, invalid)

    def test_bad_later_source_prevents_writes(self):
        self.data['atlases']['forest-temperate']['cells']['cell-2'] = {'source': 'bad', 'sha256': 'bad'}
        self.write_catalog()
        with self.assertRaises(ValueError):
            export(self.root, self.catalog)
        self.assertFalse((self.env / 'runtime').exists())

    def test_unknown_cell_and_atlas_rejected(self):
        atlas = self.data['atlases']['forest-temperate']
        atlas['cells']['unknown'] = atlas['cells']['cell-0']
        self.write_catalog()
        with self.assertRaisesRegex(ValueError, 'unknown'):
            export(self.root, self.catalog)
        self.data['atlases'] = {'surface-meadow': atlas}
        self.write_catalog()
        with self.assertRaisesRegex(ValueError, 'unsupported'):
            export(self.root, self.catalog)

    def test_baseline_hash_checked(self):
        self.data['atlases']['forest-temperate']['baselines']['1x']['sha256'] = 'bad'
        self.write_catalog()
        with self.assertRaisesRegex(ValueError, 'SHA-256'):
            export(self.root, self.catalog)

    def test_changed_anchor_rejected(self):
        data = json.loads(self.manifest.read_text())
        data['atlases'][0]['cells'][0]['anchor'] = [48, 90]
        self.manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'anchors'):
            export(self.root, self.catalog)

    def test_stale_output_rejected(self):
        result = export(self.root, self.catalog)
        Image.new('RGBA', (384, 384)).save(self.root / result[0]['output'])
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.catalog, True)


if __name__ == '__main__':
    unittest.main()
