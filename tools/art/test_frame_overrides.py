import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from export_overrides import PACKS
from export_frame_overrides import export, ORDERS


class FrameTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        for i, (campaign, directory, suffix) in enumerate(PACKS):
            pack = self.root / 'packages' / campaign / 'assets' / directory
            pack.mkdir(parents=True)
            manifest = pack / f'manifest-final-{suffix}-v1.json'
            manifest.write_text('{"assets":[]}')
            if i == 0:
                self.pack, self.manifest = pack, manifest
        self.asset = {'topicId': 'pilot', 'category': 'combat-unit', 'width': 128, 'height': 48,
                      'frames': 4, 'frameWidth': 32, 'frameHeight': 48, 'anchor': [16, 47],
                      'frameOrder': ORDERS['combat-unit'], 'png': 'runtime/combat-unit/pilot.png'}
        self.manifest.write_text(json.dumps({'assets': [self.asset]}))
        (self.pack / 'manifest-tactical-runtime-hd.json').write_text(json.dumps({'assets': [
            {'topicId': 'pilot', 'width': 256, 'height': 96, 'pixelDensity': 2,
             'png': 'runtime-hd/combat-unit/pilot.png'}]}))
        frames = {}
        for i, name in enumerate(ORDERS['combat-unit']):
            source = Image.new('RGBA', (30, 46))
            source.paste((50 + i * 20, 80, 100, 253), (4, 4, 26, 44))
            source.putpixel((3, 4), (60, 80, 100, 100))
            path = self.pack / 'masters/pilot' / f'{name}.png'
            path.parent.mkdir(parents=True, exist_ok=True)
            source.save(path)
            frames[name] = {'source': str(path.relative_to(self.root)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        self.entry = {'kind': 'frames-v1', 'frames': frames, 'placement': {'renderWidth': 30, 'origin': [1, 1]}}
        self.catalog = self.root / 'catalog.json'
        self.write()

    def write(self):
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': {'pilot': self.entry}}))

    def test_pack_order_sizes_alpha_reproducibility(self):
        before = self.manifest.read_bytes()
        report = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in report], [[128, 48], [256, 96]])
        self.assertEqual([x['pose'] for x in report[0]['frames']], ORDERS['combat-unit'])
        with Image.open(self.root / report[0]['output']) as im:
            for i in range(4):
                self.assertEqual(im.getpixel((i * 32 + 5, 5)), (50 + i * 20, 80, 100, 253))
                self.assertEqual(im.getpixel((i * 32 + 4, 5)), (60, 80, 100, 100))
        self.assertEqual(self.manifest.read_bytes(), before)
        export(self.root, self.catalog, True)

    def test_duplicate_source_pose_rejected(self):
        self.entry['frames']['walk-b'] = self.entry['frames']['idle']
        self.write()
        with self.assertRaisesRegex(ValueError, 'duplicate decoded'):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())

    def test_exact_semantic_set_required(self):
        self.entry['frames']['walking'] = self.entry['frames'].pop('walk-a')
        self.write()
        with self.assertRaisesRegex(ValueError, 'semantic'):
            export(self.root, self.catalog)

    def test_canvas_mismatch_rejected(self):
        entry = self.entry['frames']['walk-a']
        path = self.root / entry['source']
        with Image.open(path) as im:
            im.resize((60, 92)).save(path)
        entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        self.write()
        with self.assertRaisesRegex(ValueError, 'canvas dimensions'):
            export(self.root, self.catalog)

    def test_shared_placement_cannot_clip(self):
        self.entry['placement']['origin'] = [3, 1]
        self.write()
        with self.assertRaisesRegex(ValueError, 'clip'):
            export(self.root, self.catalog)

    def test_frame_boundary_gate(self):
        entry = self.entry['frames']['walk-a'];path = self.root / entry['source']
        with Image.open(path) as im:
            im.putpixel((0, 20), (10, 20, 30, 255));im.save(path)
        entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest();self.write()
        with self.assertRaisesRegex(ValueError, 'outer edge'):
            export(self.root, self.catalog)

    def test_source_hash_rejected(self):
        self.entry['frames']['attack']['sha256'] = 'bad';self.write()
        with self.assertRaisesRegex(ValueError, 'SHA-256'):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())

    def test_mission_and_structure_semantic_contracts(self):
        frames = list(self.entry['frames'].values())
        (self.pack / 'manifest-tactical-runtime-hd.json').unlink()
        for category, side in [('mission-unit', 32), ('interactive-structure', 64)]:
            order = ORDERS[category]
            height = 64 if category == 'interactive-structure' else 48
            self.asset.update(category=category, width=side * len(order), height=height,
                              frames=len(order), frameWidth=side, frameHeight=height,
                              anchor=[side // 2, height - 1], png=f'runtime/{category}/pilot.png')
            self.asset.pop('frameOrder', None)
            self.asset['stateOrder' if category == 'interactive-structure' else 'frameOrder'] = order
            self.manifest.write_text(json.dumps({'assets': [self.asset]}))
            self.entry['frames'] = dict(zip(order, frames));self.write()
            report = export(self.root, self.catalog)
            self.assertEqual(report[0]['size'], [side * len(order), height])
            self.assertEqual([r['pose'] for r in report[0]['frames']], order)
            export(self.root, self.catalog, True)

    def test_stale_frame_export_rejected(self):
        result = export(self.root, self.catalog)
        Image.new('RGBA', (128, 48)).save(self.root / result[0]['output'])
        with self.assertRaisesRegex(ValueError, 'stale'):
            export(self.root, self.catalog, True)


if __name__ == '__main__':
    unittest.main()
