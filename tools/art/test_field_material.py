import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_field_material import PACK, MANIFEST, export, match_period_edges


class FieldMaterialTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.pack = self.root / PACK
        self.manifest = self.root / MANIFEST
        self.manifest.parent.mkdir(parents=True)
        environment = self.pack / 'environment-builder-v1'
        environment.mkdir()
        (environment / 'manifest-environment-builder-v1.json').write_text(json.dumps({
            'atlases': [{'id': 'surface-meadow', 'category': 'base-surface', 'variants': 4}]}))
        self.source = self.pack / 'masters/approved/grass.png'
        self.source.parent.mkdir(parents=True)
        image = Image.new('RGB', (32, 32))
        for y in range(32):
            for x in range(32):
                image.putpixel((x, y), (x * 5, y * 6, 80))
        image.save(self.source)
        self.data = {'schemaVersion': 1, 'id': 'meadow-base', 'replaces': {'atlas': 'surface-meadow', 'tone': 0},
                     'source': 'masters/approved/grass.png', 'sourceSha256': hashlib.sha256(self.source.read_bytes()).hexdigest(),
                     'logicalSize': [16, 16], 'worldOrigin': [0, 0], 'fit': {'inset': 0, 'bleed': 0},
                     'png': 'runtime/meadow.png', 'png2x': 'runtime/meadow@2x.png'}
        self.write()

    def write(self):
        self.manifest.write_text(json.dumps(self.data))

    def test_native_density_source_immutability_and_exact_interior(self):
        before = self.source.read_bytes()
        manifest = self.manifest.read_bytes()
        reports = export(self.root, self.manifest)
        self.assertEqual(reports, export(self.root, self.manifest, True))
        self.assertEqual(before, self.source.read_bytes())
        self.assertEqual(manifest, self.manifest.read_bytes())
        for density, report in enumerate(reports, 1):
            size = 16 * density
            with Image.open(self.root / report['output']) as output, Image.open(self.source) as source:
                expected = source.resize((size, size), Image.Resampling.LANCZOS)
                self.assertEqual(output.crop((1, 1, size - 1, size - 1)).tobytes(), expected.crop((1, 1, size - 1, size - 1)).tobytes())
                self.assertEqual(output.crop((0, 0, size, 1)).tobytes(), output.crop((0, size - 1, size, size)).tobytes())
                self.assertEqual(output.crop((0, 0, 1, size)).tobytes(), output.crop((size - 1, 0, size, size)).tobytes())

    def test_edge_matching_never_mutates_input(self):
        with Image.open(self.source) as source:
            before = source.tobytes()
            match_period_edges(source)
            self.assertEqual(before, source.tobytes())

    def test_checksum_and_geometry_fail_before_writes(self):
        cases = [('sourceSha256', '0' * 64, 'SHA-256'), ('worldOrigin', [32, 0], 'zero origin'),
                 ('fit', {'inset': 3, 'bleed': .5}, 'uncropped'), ('logicalSize', [16, 8], 'aspect'),
                 ('replaces', {'atlas': 'surface-meadow', 'tone': 4}, 'existing surface')]
        original = self.data.copy()
        for key, value, error in cases:
            with self.subTest(key=key):
                self.data = {**original, key: value}
                self.write()
                with self.assertRaisesRegex(ValueError, error):
                    export(self.root, self.manifest)
                self.assertFalse((self.manifest.parent / 'runtime').exists())

    def test_output_escape_and_duplicate_are_rejected_before_writes(self):
        self.data['png2x'] = '../other.png'
        self.write()
        with self.assertRaises(ValueError):
            export(self.root, self.manifest)
        self.assertFalse((self.manifest.parent / 'runtime').exists())
        self.data['png2x'] = self.data['png']
        self.write()
        with self.assertRaisesRegex(ValueError, 'distinct'):
            export(self.root, self.manifest)
        self.assertFalse((self.manifest.parent / 'runtime').exists())

    def test_stale_export_detected(self):
        report = export(self.root, self.manifest)[1]
        target = self.root / report['output']
        with Image.open(target) as image:
            image.putpixel((4, 4), (0, 0, 0))
            image.save(target)
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.manifest, True)

    def test_alpha_is_rejected_instead_of_flattened(self):
        with Image.open(self.source) as image:
            changed = image.convert('RGBA')
            changed.putpixel((5, 5), (60, 80, 30, 254))
            changed.save(self.source)
        self.data['sourceSha256'] = hashlib.sha256(self.source.read_bytes()).hexdigest()
        self.write()
        with self.assertRaisesRegex(ValueError, 'fully opaque'):
            export(self.root, self.manifest)
        self.assertFalse((self.manifest.parent / 'runtime').exists())
