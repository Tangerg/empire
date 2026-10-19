import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_surface_overrides import PACK, ORDER, export, match_edges, seam_previews


class SurfaceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.pack = self.root / PACK
        self.env = self.pack / 'environment-builder-v1'
        self.env.mkdir(parents=True)
        self.manifest = self.env / 'manifest-environment-builder-v1.json'
        self.manifest.write_text(json.dumps({'atlases': [{
            'id': 'surface-meadow', 'columns': 4, 'rows': 1, 'cellWidth': 32,
            'cellHeight': 32, 'cellWidth2x': 64, 'cellHeight2x': 64,
            'componentCount': 4, 'category': 'base-surface', 'tileMode': 'variants',
            'variants': 4, 'edgeLocked': True, 'cellOrder': ORDER,
            'png': 'runtime/atlas/surface-meadow.png', 'png2x': 'runtime/atlas/surface-meadow@2x.png'}]}))
        cells = {}
        for i, name in enumerate(ORDER):
            image = Image.new('RGBA', (128, 128), (20 + 40 * i, 50, 80, 255))
            image.putpixel((0, 0), (230, 200, 150, 255))
            path = self.pack / 'masters/remake' / f'{name}.png'
            path.parent.mkdir(parents=True, exist_ok=True)
            image.save(path)
            cells[name] = {'source': str(path.relative_to(self.root)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        self.data = {'schemaVersion': 1, 'atlases': {'surface-meadow': {'cells': cells}}}
        self.catalog = self.root / 'catalog.json'
        self.write()

    def write(self):
        self.catalog.write_text(json.dumps(self.data))

    def test_exact_contract_reproducibility_and_interior(self):
        manifest = self.manifest.read_bytes()
        reports = export(self.root, self.catalog)
        export(self.root, self.catalog, True)
        self.assertEqual(manifest, self.manifest.read_bytes())
        for report, size in zip(reports, (32, 64)):
            self.assertEqual(report['size'], [size * 4, size])
            with Image.open(self.root / report['output']) as sheet:
                for i, name in enumerate(ORDER):
                    source = Image.open(self.root / self.data['atlases']['surface-meadow']['cells'][name]['source']).convert('RGB')
                    expected = source.resize((size, size), Image.Resampling.LANCZOS)
                    cell = sheet.crop((i * size, 0, (i + 1) * size, size))
                    self.assertEqual(cell.crop((1, 1, size - 1, size - 1)).tobytes(), expected.crop((1, 1, size - 1, size - 1)).tobytes())
                    self.assertEqual(cell.crop((0, 0, size, 1)).tobytes(), sheet.crop((0, size - 1, size, size)).tobytes())
                    self.assertEqual(cell.crop((0, 0, 1, size)).tobytes(), sheet.crop((size - 1, 0, size, size)).tobytes())

    def test_preview_repeats_exported_pixels(self):
        reports = export(self.root, self.catalog)
        for report in reports:
            sheet = Image.open(self.root / report['output'])
            size = sheet.height
            previews = seam_previews(sheet)
            for i in range(4):
                expected = sheet.crop((i * size, 0, (i + 1) * size, size)).tobytes()
                repeated = previews['repeated-2x2']
                self.assertEqual(expected, repeated.crop((i * 2 * size, size, (i * 2 + 1) * size, 2 * size)).tobytes())

    def test_matching_does_not_mutate_sources(self):
        cells = [Image.new('RGB', (64, 64), (i * 40, 20, 50)) for i in range(4)]
        before = [c.tobytes() for c in cells]
        match_edges(cells)
        self.assertEqual(before, [c.tobytes() for c in cells])

    def test_partial_family_rejected_before_writes(self):
        del self.data['atlases']['surface-meadow']['cells']['meadow-4']
        self.write()
        with self.assertRaisesRegex(ValueError, 'exact four'):
            export(self.root, self.catalog)
        self.assertFalse((self.env / 'runtime').exists())

    def test_nonopaque_source_rejected_before_writes(self):
        entry = self.data['atlases']['surface-meadow']['cells']['meadow-4']
        path = self.root / entry['source']
        image = Image.open(path)
        image.putpixel((20, 20), (0, 0, 0, 254))
        image.save(path)
        entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        self.write()
        with self.assertRaisesRegex(ValueError, 'fully opaque'):
            export(self.root, self.catalog)
        self.assertFalse((self.env / 'runtime').exists())

    def test_unknown_surface_and_changed_order_rejected(self):
        self.data['atlases']['surface-earth'] = self.data['atlases'].pop('surface-meadow')
        self.write()
        with self.assertRaisesRegex(ValueError, 'unsupported'):
            export(self.root, self.catalog)
        self.data['atlases']['surface-meadow'] = self.data['atlases'].pop('surface-earth')
        self.write()
        manifest = json.loads(self.manifest.read_text())
        manifest['atlases'][0]['cellOrder'].reverse()
        self.manifest.write_text(json.dumps(manifest))
        with self.assertRaisesRegex(ValueError, 'contract'):
            export(self.root, self.catalog)

    def test_old_stone_exact_four_source_contract(self):
        manifest = json.loads(self.manifest.read_text())
        record = manifest['atlases'][0]
        record.update(id='surface-old-stone', cellOrder=[f'old-stone-{i}' for i in range(1,5)],
                      png='runtime/atlas/surface-old-stone.png', png2x='runtime/atlas/surface-old-stone@2x.png')
        self.manifest.write_text(json.dumps(manifest))
        old = self.data['atlases'].pop('surface-meadow')['cells']
        self.data['atlases']['surface-old-stone'] = {'cells': {name: old[ORDER[i]] for i,name in enumerate(record['cellOrder'])}}
        self.write()
        before = self.manifest.read_bytes()
        reports = export(self.root, self.catalog)
        export(self.root, self.catalog, True)
        self.assertEqual(before, self.manifest.read_bytes())
        self.assertEqual([r['size'] for r in reports], [[128,32],[256,64]])
        self.assertEqual([x['cellId'] for x in reports[0]['sources']], record['cellOrder'])
        self.assertTrue(all(r['atlasId'] == 'surface-old-stone' for r in reports))
        record['cellOrder'][0] = 'meadow-1'
        self.manifest.write_text(json.dumps(manifest))
        with self.assertRaisesRegex(ValueError, 'contract'):
            export(self.root, self.catalog)

    def test_graveyard_four_slots_preserve_unregistered_surface_and_source_bytes(self):
        self.verify_four_slot_surface('surface-graveyard', 'graveyard')

    def test_forge_four_slots_preserve_unregistered_surface_and_source_bytes(self):
        self.verify_four_slot_surface('surface-forge-stone', 'forge-stone')

    def verify_four_slot_surface(self, atlas_id, prefix):
        manifest = json.loads(self.manifest.read_text())
        record = {**manifest['atlases'][0], 'id': atlas_id,
                  'cellOrder': [f'{prefix}-{i}' for i in range(1, 5)],
                  'png': f'runtime/atlas/{atlas_id}.png', 'png2x': f'runtime/atlas/{atlas_id}@2x.png'}
        manifest['atlases'].append(record)
        self.manifest.write_text(json.dumps(manifest))
        old = self.data['atlases'].pop('surface-meadow')['cells']
        self.data['atlases'][atlas_id] = {'cells': {name: old[ORDER[i]] for i, name in enumerate(record['cellOrder'])}}
        self.write()
        untouched = self.env / 'runtime/atlas/surface-meadow.png'
        untouched.parent.mkdir(parents=True)
        untouched.write_bytes(b'unregistered-existing-atlas')
        before = {entry['source']: (self.root / entry['source']).read_bytes() for entry in old.values()}
        manifest_before = self.manifest.read_bytes()
        reports = export(self.root, self.catalog)
        self.assertEqual(reports, export(self.root, self.catalog, True))
        self.assertEqual(self.manifest.read_bytes(), manifest_before)
        self.assertEqual(untouched.read_bytes(), b'unregistered-existing-atlas')
        self.assertEqual([r['size'] for r in reports], [[128, 32], [256, 64]])
        self.assertEqual([cell['cellId'] for cell in reports[0]['sources']], record['cellOrder'])
        for source, raw in before.items():
            self.assertEqual((self.root / source).read_bytes(), raw)
        for report, size in zip(reports, (32, 64)):
            with Image.open(self.root / report['output']) as sheet:
                for index, entry in enumerate(self.data['atlases'][atlas_id]['cells'].values()):
                    with Image.open(self.root / entry['source']) as source:
                        expected = source.convert('RGB').resize((size, size), Image.Resampling.LANCZOS)
                    cell = sheet.crop((index * size, 0, (index + 1) * size, size))
                    self.assertEqual(cell.crop((1, 1, size - 1, size - 1)).tobytes(), expected.crop((1, 1, size - 1, size - 1)).tobytes())
        record['cellOrder'].reverse()
        self.manifest.write_text(json.dumps(manifest))
        with self.assertRaisesRegex(ValueError, 'contract'):
            export(self.root, self.catalog)
