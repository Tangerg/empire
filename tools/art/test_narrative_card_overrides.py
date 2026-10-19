import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from export_overrides import PACKS, export


ROOT = Path(__file__).resolve().parents[2]
PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')
TOPIC = 'C01-CHAR-LEIA-01'


class NarrativeCardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.pack = self.root / PACK
        self.catalog = self.root / 'catalog.json'
        actual = json.loads((ROOT / PACK / 'manifest-final-fantasy-v1.json').read_text())
        self.assets = [a for a in actual['assets'] if a['category'] == 'narrative-static']
        self.assertEqual(len(self.assets), 80)
        for campaign, directory, suffix in PACKS:
            pack = self.root / 'packages' / campaign / 'assets' / directory
            pack.mkdir(parents=True)
            (pack / f'manifest-final-{suffix}-v1.json').write_text(json.dumps({
                'assets': self.assets if pack == self.pack else []}))
        for index, asset in enumerate(self.assets):
            target = self.pack / asset['png']
            target.parent.mkdir(parents=True, exist_ok=True)
            Image.new('RGBA', (asset['width'], asset['height']), (index, 20, 30, 255)).save(target)
        self.manifest = self.pack / 'manifest-final-fantasy-v1.json'
        self.character = self.pack / 'masters/narrative-test/character.png'
        self.background = self.character.with_name('background.png')
        self.character.parent.mkdir(parents=True)
        character = Image.new('RGBA', (192, 224), (0, 0, 0, 0))
        character.paste((150, 50, 10, 255), (40, 10, 150, 215))
        character.putpixel((35, 20), (200, 80, 40, 128))
        character.putpixel((0, 100), (120, 60, 20, 1))
        character.save(self.character)
        Image.new('RGB', (192, 224), (20, 40, 60)).save(self.background)
        self.override = {'kind': 'character-card-composite-v1',
                         'character': self.ref(self.character), 'background': self.ref(self.background),
                         'placement': {'renderWidth': 192, 'origin': [0, 0]}}
        self.register()

    def ref(self, path):
        return {'source': str(path.relative_to(self.root)),
                'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

    def register(self, entries=None):
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': entries or {TOPIC: self.override}}))

    def outputs(self):
        return {a['topicId']: (self.pack / a['png']).read_bytes() for a in self.assets}

    def mutate_asset(self, topic, **values):
        data = json.loads(self.manifest.read_text())
        next(a for a in data['assets'] if a['topicId'] == topic).update(values)
        self.manifest.write_text(json.dumps(data))

    def test_composes_real_alpha_once_and_retains_both_complete_sources(self):
        native = {p: p.read_bytes() for p in (self.character, self.background)}
        manifests = {p: p.read_bytes() for p in self.root.rglob('*.json')}
        before = self.outputs()
        report = export(self.root, self.catalog)
        self.assertEqual(len(report), 1)
        result = report[0]
        self.assertEqual(result['size'], [192, 224])
        self.assertEqual(result['sources']['character']['sha256'], self.ref(self.character)['sha256'])
        self.assertEqual(result['sources']['character']['alpha']['edgeMaxAlpha'], 1)
        self.assertEqual(result['characterPlacement']['crop'], None)
        self.assertEqual(result['backgroundFit']['sourceCropBox'], [0, 0, 192, 224])
        with Image.open(self.root / result['output']) as card:
            self.assertEqual(card.mode, 'RGBA')
            self.assertEqual(card.getchannel('A').getextrema(), (255, 255))
            self.assertEqual(card.getpixel((0, 0)), (20, 40, 60, 255))
            self.assertEqual(card.getpixel((50, 20)), (150, 50, 10, 255))
            # Original half-alpha edge is composited once, not squared by paste(mask).
            self.assertEqual(card.getpixel((35, 20)), (110, 60, 50, 255))
        self.assertEqual(native, {p: p.read_bytes() for p in native})
        self.assertEqual(manifests, {p: p.read_bytes() for p in manifests})
        after = self.outputs()
        self.assertEqual([topic for topic in before if before[topic] != after[topic]], [TOPIC])
        self.assertEqual(export(self.root, self.catalog, check=True), report)
        export(self.root, self.catalog)
        self.assertEqual(self.outputs(), after)

    def test_two_registered_cards_leave_other_78_outputs_unchanged(self):
        before = self.outputs()
        second = 'C01-CHAR-MIREL-01'
        self.register({TOPIC: self.override, second: self.override})
        result = export(self.root, self.catalog)
        self.assertEqual({r['topicId'] for r in result}, {TOPIC, second})
        self.assertEqual({t for t, b in before.items() if self.outputs()[t] != b}, {TOPIC, second})

    def test_background_cover_crop_is_explicit_and_character_canvas_has_no_bbox_fit(self):
        Image.new('RGBA', (256, 256), (20, 40, 60, 255)).save(self.background)
        self.override['background'] = self.ref(self.background)
        self.override['placement'] = {'renderWidth': 96, 'origin': [13, 17]}
        self.register()
        result = export(self.root, self.catalog)[0]
        self.assertEqual(result['characterPlacement']['renderSize'], [96, 112])
        crop = result['backgroundFit']['sourceCropBox']
        self.assertAlmostEqual(crop[0], 128 / 7)
        self.assertEqual(crop[1], 0)
        self.assertAlmostEqual(crop[2], 256 - 128 / 7)
        self.assertEqual(crop[3], 256)
        with Image.open(self.root / result['output']) as card:
            self.assertEqual(card.getpixel((13, 17)), (20, 40, 60, 255))
            self.assertEqual(card.getpixel((40, 40)), (150, 50, 10, 255))

    def test_unknown_topic_and_wrong_owned_contracts_are_rejected(self):
        for topic in ['unknown', 'C01-CH01-S00', 'C01-CH03-S11', 'C01-CH01-S01']:
            with self.subTest(topic=topic):
                self.register({topic: self.override})
                before = self.outputs()
                with self.assertRaises(ValueError):
                    export(self.root, self.catalog)
                self.assertEqual(self.outputs(), before)

    def test_opaque_or_rgb_character_is_rejected_even_with_valid_background(self):
        for mode, alpha in [('RGB', 255), ('RGBA', 255), ('RGBA', 0)]:
            with self.subTest(mode=mode, alpha=alpha):
                Image.new(mode, (192, 224), (20, 30, 40, alpha) if mode == 'RGBA' else (20, 30, 40)).save(self.character)
                self.override['character'] = self.ref(self.character)
                self.register()
                with self.assertRaisesRegex(ValueError, 'RGBA|transparent exterior'):
                    export(self.root, self.catalog)

    def test_transparent_loose_background_or_palette_mode_is_not_accepted(self):
        for mode in ['RGBA', 'P']:
            with self.subTest(mode=mode):
                Image.new(mode, (192, 224)).save(self.background)
                self.override['background'] = self.ref(self.background)
                self.register()
                with self.assertRaisesRegex(ValueError, 'background must'):
                    export(self.root, self.catalog)

    def test_legacy_one_source_spec_cannot_implicitly_become_a_card(self):
        self.register({TOPIC: self.ref(self.character)})
        with self.assertRaisesRegex(ValueError, 'character-card-composite-v1'):
            export(self.root, self.catalog)

    def test_placement_rejects_clipping_nonfinite_bool_or_fractional_coordinates(self):
        for placement in [{'renderWidth': 193, 'origin': [0, 0]},
                          {'renderWidth': 192, 'origin': [1, 0]},
                          {'renderWidth': 192, 'origin': [0, 1]},
                          {'renderWidth': 192, 'origin': [-1, 0]},
                          {'renderWidth': float('nan'), 'origin': [0, 0]},
                          {'renderWidth': True, 'origin': [0, 0]},
                          {'renderWidth': 192, 'origin': [0.5, 0]},
                          {'renderWidth': 192, 'origin': [0, False]}]:
            with self.subTest(placement=placement):
                self.override['placement'] = placement
                self.register()
                with self.assertRaisesRegex(ValueError, 'placement|complete character'):
                    export(self.root, self.catalog)

    def test_later_invalid_background_hash_prevents_all_writes(self):
        before = self.outputs()
        bad = copy.deepcopy(self.override)
        bad['background']['sha256'] = '0' * 64
        self.register({TOPIC: self.override, 'C01-CHAR-MIREL-01': bad})
        with self.assertRaisesRegex(ValueError, 'background: source SHA-256'):
            export(self.root, self.catalog)
        self.assertEqual(self.outputs(), before)

    def test_wrong_character_hash_and_cross_pack_sources_rejected(self):
        self.override['character']['sha256'] = '0' * 64
        self.register()
        with self.assertRaisesRegex(ValueError, 'character: source SHA-256'):
            export(self.root, self.catalog)
        self.override['character'] = self.ref(self.character)
        self.override['background']['source'] = 'packages/story-candidate-02/assets/final-scifi-v1/masters/other.png'
        self.register()
        with self.assertRaisesRegex(ValueError, 'pack masters'):
            export(self.root, self.catalog)

    def test_dimensions_hd_and_duplicate_destinations_rejected(self):
        self.mutate_asset(TOPIC, width=128)
        with self.assertRaisesRegex(ValueError, '192x224'):
            export(self.root, self.catalog)
        self.mutate_asset(TOPIC, width=192)
        hd = self.pack / 'manifest-tactical-runtime-hd.json'
        hd.write_text(json.dumps({'assets': [{'topicId': TOPIC, 'width': 384, 'height': 448}]}))
        with self.assertRaisesRegex(ValueError, '192x224'):
            export(self.root, self.catalog)
        hd.unlink()
        target = next(a['png'] for a in self.assets if a['topicId'] == TOPIC)
        self.mutate_asset('C01-CHAR-MIREL-01', png=target)
        self.register({TOPIC: self.override, 'C01-CHAR-MIREL-01': self.override})
        with self.assertRaisesRegex(ValueError, 'duplicate destination'):
            export(self.root, self.catalog)

    def test_stale_composite_check_does_not_repair_or_write(self):
        report = export(self.root, self.catalog)[0]
        target = self.root / report['output']
        Image.new('RGBA', (192, 224)).save(target)
        before = target.read_bytes()
        with self.assertRaisesRegex(ValueError, 'stale export'):
            export(self.root, self.catalog, check=True)
        self.assertEqual(target.read_bytes(), before)

    def test_character_card_cannot_write_outside_owned_runtime_directory(self):
        self.mutate_asset(TOPIC, png='runtime/battle-prop/not-a-card.png')
        with self.assertRaisesRegex(ValueError, 'unexpected export path'):
            export(self.root, self.catalog)

    def test_other_campaign_card_is_outside_this_bounded_contract(self):
        campaign, directory, suffix = PACKS[1]
        pack = self.root / 'packages' / campaign / 'assets' / directory
        other = copy.deepcopy(next(a for a in self.assets if a['topicId'] == TOPIC))
        other['topicId'] = 'C02-test-card'
        (pack / f'manifest-final-{suffix}-v1.json').write_text(json.dumps({'assets': [other]}))
        self.register({other['topicId']: self.override})
        with self.assertRaisesRegex(ValueError, 'Gray Flag'):
            export(self.root, self.catalog)


if __name__ == '__main__':
    unittest.main()
