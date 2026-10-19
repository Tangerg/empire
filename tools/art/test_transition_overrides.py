import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_transition_overrides import PACK, MASKS, export


class TransitionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.pack = self.root / PACK
        self.env = self.pack / 'environment-builder-v1'
        self.env.mkdir(parents=True)
        self.manifest = self.env / 'manifest-environment-builder-v1.json'
        self.manifest.write_text(json.dumps({'atlases': [{
            'id': 'transition-meadow-forest', 'columns': 16, 'rows': 3, 'cellWidth': 32,
            'cellHeight': 32, 'cellWidth2x': 64, 'cellHeight2x': 64, 'componentCount': 47,
            'tileMode': 'blob-47', 'maskOrder': MASKS, 'bitOrder': ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'],
            'baseSurface': 'surface-meadow', 'overlaySurface': 'surface-forest-floor',
            'png': 'runtime/atlas/transition-meadow-forest.png', 'png2x': 'runtime/atlas/transition-meadow-forest@2x.png'}]}))
        entry = self.save(Image.new('RGB', (128, 128), (80, 90, 60)), 'material.png')
        baselines = {}
        for density, size in ((1, 32), (2, 64)):
            image = Image.new('RGBA', (size * 16, size * 3), (12, 13, 14, 15))
            for i in range(47):
                x, y = i % 16 * size, i // 16 * size
                image.paste((30, 40, 50, (i * 7) % 256), (x + 2, y + 2, x + size - 2, y + size - 2))
            baselines[f'{density}x'] = self.save(image, f'baseline-{density}.png')
        self.data = {'schemaVersion': 1, 'atlases': {'transition-meadow-forest': {**entry, 'baselines': baselines}}}
        self.catalog = self.root / 'catalog.json'
        self.write()

    def save(self, image, name):
        path = self.pack / 'masters/remake' / name
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path)
        return {'source': str(path.relative_to(self.root)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

    def write(self):
        self.catalog.write_text(json.dumps(self.data))

    def test_masks_unused_cell_and_manifest_are_unchanged(self):
        before = self.manifest.read_bytes()
        reports = export(self.root, self.catalog)
        export(self.root, self.catalog, True)
        self.assertEqual(before, self.manifest.read_bytes())
        for report in reports:
            base = Image.open(self.root / report['baseline']['source'])
            output = Image.open(self.root / report['output'])
            self.assertEqual(base.getchannel('A').tobytes(), output.getchannel('A').tobytes())
            size = report['density'] * 32
            box = (15 * size, 2 * size, 16 * size, 3 * size)
            self.assertEqual(base.crop(box).tobytes(), output.crop(box).tobytes())
            self.assertEqual([m['mask'] for m in report['masks']], MASKS)
            self.assertEqual(output.getpixel((size // 2, size // 2))[:3], (80, 90, 60))

    def test_baseline_hash_failure_prevents_all_writes(self):
        self.data['atlases']['transition-meadow-forest']['baselines']['2x']['sha256'] = 'bad'
        self.write()
        with self.assertRaisesRegex(ValueError, 'SHA-256'):
            export(self.root, self.catalog)
        self.assertFalse((self.env / 'runtime').exists())

    def test_nonopaque_material_rejected(self):
        entry = self.data['atlases']['transition-meadow-forest']
        replacement = self.save(Image.new('RGBA', (128, 128), (80, 90, 60, 254)), 'transparent.png')
        entry.update(replacement)
        self.write()
        with self.assertRaisesRegex(ValueError, 'opaque'):
            export(self.root, self.catalog)
        self.assertFalse((self.env / 'runtime').exists())

    def test_changed_mask_order_rejected(self):
        data = json.loads(self.manifest.read_text())
        data['atlases'][0]['maskOrder'].reverse()
        self.manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'contract'):
            export(self.root, self.catalog)
