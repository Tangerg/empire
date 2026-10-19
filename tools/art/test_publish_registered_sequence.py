import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from publish_registered_sequence import publish, PACK


class PublishSequenceTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.pack = self.root / PACK
        self.pack.mkdir(parents=True)
        self.export = self.root / 'export'
        self.export.mkdir()
        record = {'topicId': 'unit', 'category': 'combat-unit', 'png': 'runtime/unit.png',
                  'frames': 4, 'frameOrder': ['idle', 'walk-a', 'attack', 'walk-b'], 'footprint': [1, 1]}
        (self.pack / 'manifest-final-fantasy-v1.json').write_text(json.dumps({'assets': [record]}))
        (self.pack / 'manifest-tactical-runtime-hd.json').write_text(json.dumps({'assets': [{'topicId': 'unit'}]}))
        self.sequence = {'config': {'frames': {k: 'source' for k in record['frameOrder']}, 'frameSize': [12, 16]}, 'logicalPivot': [6, 14]}
        self.write_sequence()
        for density in (1, 2):
            image = Image.new('RGBA', (48 * density, 16 * density))
            image.paste((50, 80, 40, 255), (density, density, 10 * density, 14 * density))
            image.save(self.export / f'sheet@{density}x.png')

    def write_sequence(self):
        (self.export / 'sequence.json').write_text(json.dumps(self.sequence))

    def test_preserves_semantics_and_publishes_identical_bytes(self):
        result = publish('unit', self.export, self.root)
        self.assertEqual(result['footprint'], [1, 1])
        self.assertEqual(result['anchor'], [6, 14])
        for density, key in ((1, 'png'), (2, 'hdPng')):
            self.assertEqual((self.pack / result[key]).read_bytes(), (self.export / f'sheet@{density}x.png').read_bytes())
        record = json.loads((self.pack / 'manifest-final-fantasy-v1.json').read_text())['assets'][0]
        self.assertTrue(record['preserveAuthoredPalette'])
        self.assertEqual(record['frameOrder'], list(self.sequence['config']['frames']))

    def test_rejects_wrong_order_before_writes(self):
        before = (self.pack / 'manifest-final-fantasy-v1.json').read_bytes()
        self.sequence['config']['frames'] = {'walk-a': '', 'idle': '', 'attack': '', 'walk-b': ''}
        self.write_sequence()
        with self.assertRaisesRegex(ValueError, 'semantic order'):
            publish('unit', self.export, self.root)
        self.assertEqual(before, (self.pack / 'manifest-final-fantasy-v1.json').read_bytes())

    def test_rejects_wrong_sheet_before_writes(self):
        Image.new('RGBA', (3, 3)).save(self.export / 'sheet@2x.png')
        with self.assertRaisesRegex(ValueError, 'geometry'):
            publish('unit', self.export, self.root)
        self.assertFalse((self.pack / 'runtime-approved-v5').exists())

    def test_mission_preserves_authored_palette_and_work_order(self):
        path = self.pack / 'manifest-final-fantasy-v1.json'
        document = json.loads(path.read_text())
        record = document['assets'][0]
        record['category'] = 'mission-unit'
        record['frameOrder'] = ['idle', 'walk-a', 'carry-or-work', 'walk-b']
        path.write_text(json.dumps(document))
        self.sequence['config']['frames'] = {k: 'source' for k in record['frameOrder']}
        self.write_sequence()
        publish('unit', self.export, self.root)
        actual = json.loads(path.read_text())['assets'][0]
        self.assertTrue(actual['preserveAuthoredPalette'])
        self.assertEqual(actual['frameOrder'], record['frameOrder'])
