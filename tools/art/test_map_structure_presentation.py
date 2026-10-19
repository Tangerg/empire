"""Synthetic masks exercise the gate; no production source pixels are authored."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from check_map_structure_presentation import PACK, STATES, verify


class MapStructurePresentationContract(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.export = self.root / 'export'
        self.export.mkdir()
        self.entry = {'id': 'field-post-compact', 'topicId': 'C01-STRUCT-REFUGEE-SHELTER',
                      'frameWidth': 64, 'frameHeight': 64, 'frames': 3, 'stateOrder': STATES,
                      'pixelDensity': 2, 'hdWidth': 384, 'hdHeight': 128,
                      'anchor': [32, 60], 'footprint': [1, 1], 'presentationScale': .5,
                      'png': 'runtime-approved-v5/map-structure-presentation/field-post-compact.png',
                      'hdPng': 'runtime-approved-v5/map-structure-presentation/field-post-compact@2x.png'}
        frames = {}
        provenance = []
        for index, state in enumerate(STATES):
            source = Path('sources') / f'{state}.png'
            (self.root / source).parent.mkdir(exist_ok=True)
            Image.new('RGBA', (64, 64), (index, 0, 0, 128)).save(self.root / source)
            frames[state] = str(source)
            for density in (1, 2):
                provenance.append({'frame': state, 'density': density, 'source': str(source),
                                   'registrationOffset': [0, 0], 'sourceSha256': hashlib.sha256((self.root / source).read_bytes()).hexdigest()})
        self.sequence = {'config': {'frames': frames, 'frameSize': [64, 64], 'scale': 1, 'origin': [0, 0], 'sourceFoot': [32, 60]},
                         'logicalPivot': [32, 60], 'independentFrameFit': False,
                         'gameplayFootprintChanged': False, 'frames': provenance}
        self.save_sequence()
        for density, key in [(1, 'png'), (2, 'hdPng')]:
            image = Image.new('RGBA', (192*density, 64*density))
            for index in range(3):
                image.paste((index, 20, 30, 255), ((64*index+4)*density, 8*density, (64*index+60)*density, 60*density))
            image.save(self.export / f'sheet@{density}x.png')
            target = self.root / PACK / self.entry[key]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((self.export / f'sheet@{density}x.png').read_bytes())

    def save_sequence(self):
        (self.export / 'sequence.json').write_text(json.dumps(self.sequence))

    def check(self):
        return verify(self.entry, self.export, [-14, -26, 14, 0], root=self.root)

    def mutate_pixel(self, point, alpha):
        for path in [self.export / 'sheet@1x.png', self.root / PACK / self.entry['png']]:
            with Image.open(path) as original:
                image = original.copy()
            image.putpixel(point, (1, 2, 3, alpha))
            image.save(path)

    def test_exact_boundary_and_native_hd_provenance(self):
        result = self.check()
        self.assertEqual(len(result['frames']), 6)
        self.assertEqual(result['canonicalTopicsAdded'], 0)
        self.assertTrue(all(frame['alphaBounds']['2']['worldBounds'] == [-14, -26, 14, 0] for frame in result['frames']))

    def test_dust_is_reported_without_cutting_it_out(self):
        self.mutate_pixel((2, 61), 1)
        result = self.check()
        self.assertEqual(result['frames'][0]['alphaBounds']['1']['worldBounds'], [-15, -26, 14, 1])
        self.assertEqual(result['enforcedAlphaThreshold'], 2)

    def test_semtransparent_shadow_outside_support_fails(self):
        self.mutate_pixel((2, 61), 2)
        with self.assertRaisesRegex(ValueError, 'exceeds world support'):
            self.check()

    def test_transparent_derived_perimeter_is_mandatory(self):
        self.mutate_pixel((0, 10), 1)
        with self.assertRaisesRegex(ValueError, 'transparent perimeter'):
            self.check()

    def test_changed_runtime_bytes_fail(self):
        (self.root / PACK / self.entry['png']).write_bytes(b'wrong')
        with self.assertRaisesRegex(ValueError, 'approved export bytes'):
            self.check()

    def test_stale_native_source_hash_fails(self):
        (self.root / self.sequence['config']['frames']['normal']).write_bytes(b'wrong')
        with self.assertRaisesRegex(ValueError, 'provenance hash mismatch'):
            self.check()

    def test_wrong_state_order_or_pivot_fails(self):
        self.entry['stateOrder'] = ['normal', 'captured', 'damaged']
        with self.assertRaisesRegex(ValueError, 'three normal'):
            self.check()
        self.entry['stateOrder'] = STATES
        self.entry['anchor'] = [32, 59]
        with self.assertRaisesRegex(ValueError, 'registered geometry'):
            self.check()

    def test_clipped_source_canvas_fails_even_if_export_fits(self):
        self.sequence['config']['scale'] = 1.01
        self.sequence['config']['sourceFoot'] = [32 / 1.01, 60 / 1.01]
        self.save_sequence()
        with self.assertRaisesRegex(ValueError, 'clips full native'):
            self.check()

    def test_missing_density_provenance_fails(self):
        self.sequence['frames'].pop()
        self.save_sequence()
        with self.assertRaisesRegex(ValueError, 'complete ordered'):
            self.check()


if __name__ == '__main__':
    unittest.main()
