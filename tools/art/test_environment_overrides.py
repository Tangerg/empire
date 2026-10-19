import copy
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
        entry['registration'] = {'scale': 0.5, 'origin': [5, 5], 'sourceFoot': [86, 172]}
        self.data = {'schemaVersion': 2, 'atlases': {'forest-temperate': {
            'baselines': baselines, 'cells': {'cell-0': entry, 'cell-2': dict(entry)}}}}
        self.catalog = self.root / 'catalog.json'
        self.write_catalog()

    def save(self, image, name):
        path = self.pack / 'masters/remake' / name
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path)
        return {'source': str(path.relative_to(self.root)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

    def write_catalog(self):
        self.catalog.write_text(json.dumps(self.data))

    def configure_rural(self):
        # Use the shipped semantic metadata rather than a parallel rural catalog.
        path = Path(__file__).resolve().parents[2] / PACK / 'environment-builder-v1/manifest-environment-builder-v1.json'
        atlas = next(a for a in json.loads(path.read_text())['atlases'] if a['id'] == 'rural-life')
        baselines = {}
        for density, size in ((1, 112), (2, 224)):
            image = Image.new('RGBA', (size * 4, size * 4))
            for i in range(16):
                image.paste((i * 10, 30, 60, 200), cell_box(i, size))
            baselines[f'{density}x'] = self.save(image, f'rural-base-{density}.png')
        cells = {}
        for index, cell_id in ((4, 'wheat-sheaves'), (5, 'wheat-bundles'),
                               (6, 'grain-sacks-baskets'), (14, 'farm-handcart-tools')):
            self.assertEqual(atlas['cellOrder'][index], cell_id)
            atlas['cells'][index]['anchor'] = [56, 108]
            source = Image.new('RGBA', (208, 208))
            source.paste((index * 10, 100, 60, 253), (10, 10, 198, 198))
            source.putpixel((0, 20), (30, 50, 70, 1))
            cells[cell_id] = self.save(source, f'{cell_id}.png')
            cells[cell_id]['registration'] = {'scale': 0.5, 'origin': [4, 4], 'sourceFoot': [104, 208]}
        self.manifest.write_text(json.dumps({'atlases': [atlas]}))
        self.data = {'schemaVersion': 2, 'atlases': {'rural-life': {'baselines': baselines, 'cells': cells}}}
        self.write_catalog()

    def test_rural_exact_slots_sizes_baselines_and_semantics(self):
        self.configure_rural()
        manifest_before = self.manifest.read_bytes()
        masters_before = {p: p.read_bytes() for p in (self.pack / 'masters').rglob('*.png')}
        reports = export(self.root, self.catalog)
        export(self.root, self.catalog, True)
        self.assertEqual(self.manifest.read_bytes(), manifest_before)
        self.assertEqual({p: p.read_bytes() for p in masters_before}, masters_before)
        for report, size in zip(reports, (112, 224)):
            self.assertEqual(report['output'], str(PACK / 'environment-builder-v1/runtime/atlas' /
                                                  f'rural-life{"@2x" if size == 224 else ""}.png'))
            with Image.open(self.root / report['output']) as output:
                self.assertEqual(output.size, (size * 4, size * 4))
                self.assertEqual(output.mode, 'RGBA')
                for cell in report['cells']:
                    if cell['index'] not in (4, 5, 6, 14):
                        self.assertFalse(cell['overridden'])
                        self.assertEqual(cell['baselinePixelSha256'], cell['outputPixelSha256'])
                        continue
                    self.assertTrue(cell['overridden'])
                    self.assertNotEqual(cell['baselinePixelSha256'], cell['outputPixelSha256'])
                    entry = self.data['atlases']['rural-life']['cells'][cell['id']]
                    with Image.open(self.root / entry['source']) as source:
                        expected = Image.new('RGBA', (224, 224))
                        expected.paste(source, (8, 8))
                        self.assertEqual(expected.getpixel((8, 28)), (30, 50, 70, 1))
                        if size == 112:
                            expected = expected.resize((112, 112), Image.Resampling.LANCZOS)
                        self.assertEqual(output.crop(cell_box(cell['index'], size)).tobytes(), expected.tobytes())
                    self.assertEqual(report['sources'][cell['id']]['logicalAnchor'], [56, 108])

    def test_rural_registration_changes_only_selected_anchor(self):
        self.configure_rural()
        expected = json.loads(self.manifest.read_text())
        expected['atlases'][0]['cells'][14]['anchor'] = [56, 106]
        self.data['atlases']['rural-life']['cells']['farm-handcart-tools']['registration']['sourceFoot'] = [104, 204]
        self.write_catalog()
        export(self.root, self.catalog)
        self.assertEqual(json.loads(self.manifest.read_text()), expected)
        export(self.root, self.catalog, True)

    def test_mixed_rural_and_forest_catalog_uses_each_manifest_geometry(self):
        forest = json.loads(self.manifest.read_text())['atlases'][0]
        forest_override = self.data['atlases']['forest-temperate']
        self.configure_rural()
        rural = json.loads(self.manifest.read_text())['atlases'][0]
        rural_override = self.data['atlases']['rural-life']
        for atlases in ([rural, forest], [forest, rural]):
            with self.subTest(order=[a['id'] for a in atlases]):
                self.manifest.write_text(json.dumps({'atlases': atlases}))
                before = self.manifest.read_bytes()
                overrides = {'rural-life': rural_override, 'forest-temperate': forest_override}
                self.data['atlases'] = {a['id']: overrides[a['id']] for a in atlases}
                self.write_catalog()
                reports = export(self.root, self.catalog)
                export(self.root, self.catalog, True)
                self.assertEqual(len(reports), 4)
                self.assertEqual(self.manifest.read_bytes(), before)
                for report in reports:
                    size = (112 if report['atlasId'] == 'rural-life' else 96) * report['density']
                    with Image.open(self.root / report['output']) as output:
                        self.assertEqual(output.size, (size * 4, size * 4))

    def test_rural_wrong_geometry_rejected_before_writes(self):
        self.configure_rural()
        original = json.loads(self.manifest.read_text())
        for key, value in [('columns', 2), ('rows', 8), ('cellWidth', 96),
                           ('cellHeight', 96), ('cellWidth2x', 192), ('cellHeight2x', 192),
                           ('anchor', [48, 91]), ('tileMode', 'terrain')]:
            with self.subTest(key=key):
                data = copy.deepcopy(original)
                data['atlases'][0][key] = value
                self.manifest.write_text(json.dumps(data))
                before = self.manifest.read_bytes()
                with self.assertRaisesRegex(ValueError, 'unsupported scenery contract'):
                    export(self.root, self.catalog)
                self.assertFalse((self.env / 'runtime').exists())
                self.assertEqual(self.manifest.read_bytes(), before)
        # A coherent but wrong 96/192 rural contract must also be rejected.
        data = copy.deepcopy(original)
        data['atlases'][0].update(cellWidth=96, cellHeight=96, cellWidth2x=192,
                                  cellHeight2x=192, anchor=[48, 91])
        self.manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'unsupported scenery contract'):
            export(self.root, self.catalog)

    def test_forest_rejects_rural_geometry(self):
        data = json.loads(self.manifest.read_text())
        data['atlases'][0].update(cellWidth=112, cellHeight=112, cellWidth2x=224,
                                  cellHeight2x=224, anchor=[56, 108])
        self.manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'unsupported scenery contract'):
            export(self.root, self.catalog)

    def test_rural_rejects_wrong_baseline_sizes_at_both_densities(self):
        self.configure_rural()
        original = copy.deepcopy(self.data)
        for density, wrong_size in ((1, 384), (2, 768)):
            with self.subTest(density=density):
                self.data = copy.deepcopy(original)
                self.data['atlases']['rural-life']['baselines'][f'{density}x'] = self.save(
                    Image.new('RGBA', (wrong_size, wrong_size)), f'wrong-{density}.png')
                self.write_catalog()
                with self.assertRaisesRegex(ValueError, 'baseline atlas dimensions'):
                    export(self.root, self.catalog)
                self.assertFalse((self.env / 'runtime').exists())

    def test_rural_bad_later_source_preserves_existing_outputs_and_metadata(self):
        self.configure_rural()
        reports = export(self.root, self.catalog)
        paths = [self.manifest, *(self.root / report['output'] for report in reports)]
        before = {p: p.read_bytes() for p in paths}
        original = copy.deepcopy(self.data)
        invalid = Image.new('RGBA', (208, 208))
        invalid.paste((30, 50, 70, 253), (10, 10, 198, 198))
        invalid.putpixel((0, 20), (30, 50, 70, 2))
        for change in [{'sha256': 'bad'}, self.save(invalid, 'clipped-source.png'),
                       {'registration': {'scale': 0.5, 'origin': [9, 4], 'sourceFoot': [104, 208]}}]:
            with self.subTest(change=change):
                self.data = copy.deepcopy(original)
                self.data['atlases']['rural-life']['cells']['farm-handcart-tools'].update(change)
                self.write_catalog()
                with self.assertRaises(ValueError):
                    export(self.root, self.catalog)
                self.assertEqual({p: p.read_bytes() for p in paths}, before)

    def test_rural_complete_nonsquare_canvas_and_projected_contact(self):
        source = Image.new('RGBA', (207, 159))
        source.paste((20, 30, 40, 253), (10, 10, 197, 149))
        source.putpixel((0, 20), (30, 50, 70, 1))
        before = source.tobytes()
        small, hd, anchor = render_cell(source, {'scale': 0.48, 'origin': [6.5, 28], 'sourceFoot': [100, 150]}, 112)
        expected = Image.new('RGBA', (224, 224))
        expected.paste(source.resize((199, 153), Image.Resampling.LANCZOS), (13, 56))
        self.assertEqual(hd.tobytes(), expected.tobytes())
        self.assertEqual(small.tobytes(), expected.resize((112, 112), Image.Resampling.LANCZOS).tobytes())
        self.assertEqual(anchor, [6.5 + 100 * 199 / (2 * 207), 28 + 150 * 153 / (2 * 159)])
        self.assertEqual(source.tobytes(), before)

    def test_rural_check_rejects_stale_pixels_at_both_densities(self):
        self.configure_rural()
        reports = export(self.root, self.catalog)
        for report in reports:
            with self.subTest(density=report['density']):
                target = self.root / report['output']
                original = target.read_bytes()
                with Image.open(target) as im:
                    altered = im.copy()
                altered.putpixel((0, 0), (1, 2, 3, 4))
                altered.save(target)
                with self.assertRaisesRegex(ValueError, 'stale environment export'):
                    export(self.root, self.catalog, True)
                target.write_bytes(original)

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

    def test_regional_scenery_preserves_other_cells_and_semantics(self):
        data = json.loads(self.manifest.read_text())
        atlas = data['atlases'][0]
        atlas['id'] = 'scenery-regional'
        atlas['category'] = 'regional-scenery'
        atlas['png'] = 'runtime/atlas/scenery-regional.png'
        atlas['png2x'] = 'runtime/atlas/scenery-regional@2x.png'
        atlas['cells'][0]['obstructionHeight'] = 2
        atlas['cells'][0]['footprint'] = [2, 2]
        self.manifest.write_text(json.dumps(data))
        before = self.manifest.read_bytes()
        self.data['atlases']['scenery-regional'] = self.data['atlases'].pop('forest-temperate')
        self.write_catalog()
        reports = export(self.root, self.catalog)
        export(self.root, self.catalog, True)
        self.assertEqual(self.manifest.read_bytes(), before)
        for report in reports:
            self.assertIn('scenery-regional', report['output'])
            for cell in report['cells']:
                if cell['index'] not in (0, 2):
                    self.assertEqual(cell['baselinePixelSha256'], cell['outputPixelSha256'])

    def test_complete_canvas_alpha_and_bottom_placement(self):
        source = Image.new('RGBA', (172, 172))
        source.paste((20, 30, 40, 253), (10, 10, 160, 160))
        source.putpixel((0, 20), (30, 50, 70, 1))
        original = source.tobytes()
        small, hd, anchor = render_cell(source, {'scale': 0.5, 'origin': [5, 5], 'sourceFoot': [86, 172]})
        self.assertEqual(anchor, [48, 91])
        self.assertEqual(hd.getpixel((10, 30)), (30, 50, 70, 1))
        self.assertEqual(hd.getpixel((20, 20)), (20, 30, 40, 253))
        self.assertEqual(source.tobytes(), original)
        self.assertEqual(small.size, (96, 96))

    def test_explicit_source_placement_preserves_alpha(self):
        entry = self.data['atlases']['forest-temperate']['cells']['cell-0']
        entry['registration'] = {'scale': 0.5, 'origin': [5, 9], 'sourceFoot': [86, 164]}
        self.write_catalog()
        reports = export(self.root, self.catalog)
        self.assertEqual(reports[0]['sources']['cell-0']['logicalAnchor'], [48, 91])
        with Image.open(self.root / entry['source']) as source:
            _, normal, _ = render_cell(source, {'scale': 0.5, 'origin': [5, 5], 'sourceFoot': [86, 172]})
            _, moved, _ = render_cell(source, entry['registration'])
            self.assertEqual(normal.crop((10, 10, 182, 182)).tobytes(),
                             moved.crop((10, 18, 182, 190)).tobytes())

    def test_stem_anchor_preserves_visible_overhang(self):
        entry = self.data['atlases']['forest-temperate']['cells']['cell-0']
        entry['registration'] = {'scale': 0.5, 'origin': [5, 5], 'sourceFoot': [86, 140]}
        self.write_catalog()
        original = json.loads(self.manifest.read_text())['atlases'][0]['cells'][0]
        export(self.root, self.catalog)
        cell = json.loads(self.manifest.read_text())['atlases'][0]['cells'][0]
        self.assertEqual(cell, {**original, 'anchor': [48, 75]})
        with Image.open(self.root / entry['source']) as source:
            logical, hd, anchor = render_cell(source, entry['registration'])
            self.assertGreater(hd.getchannel('A').getbbox()[3], anchor[1] * 2)
            self.assertGreater(logical.getchannel('A').getbbox()[3], anchor[1])
            self.assertEqual(hd.getpixel((10, 30)), (30, 50, 70, 1))

    def test_registration_rejects_clipping_and_invalid_coordinates(self):
        entry = self.data['atlases']['forest-temperate']['cells']['cell-0']
        with Image.open(self.root / entry['source']) as source:
            for change in [{'scale': 2}, {'scale': float('nan')}, {'scale': True},
                           {'origin': [-1, 5]}, {'origin': [5, 11]}, {'origin': [5, 5.25]},
                           {'sourceFoot': [86, 200]}, {'sourceFoot': [86, float('inf')]}]:
                with self.assertRaises(ValueError):
                    render_cell(source, {**entry['registration'], **change})

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
        export(self.root, self.catalog)
        data = json.loads(self.manifest.read_text())
        data['atlases'][0]['cells'][0]['anchor'] = [48, 90]
        self.manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'stale environment registration anchors'):
            export(self.root, self.catalog, True)

    def test_presentation_scale_has_one_manifest_owner(self):
        data = json.loads(self.manifest.read_text())
        data['atlases'][0]['cells'][0]['presentationScale'] = 0.65
        self.manifest.write_text(json.dumps(data))
        before = self.manifest.read_bytes()
        report = export(self.root, self.catalog)
        self.assertEqual(report[0]['sources']['cell-0']['presentationScale'], 0.65)
        self.assertEqual(self.manifest.read_bytes(), before)
        for value in [0, -1, float('nan'), float('inf'), True]:
            data['atlases'][0]['cells'][0]['presentationScale'] = value
            self.manifest.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, 'presentationScale'):
                export(self.root, self.catalog)

    def test_stale_output_rejected(self):
        result = export(self.root, self.catalog)
        Image.new('RGBA', (384, 384)).save(self.root / result[0]['output'])
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.catalog, True)


if __name__ == '__main__':
    unittest.main()
