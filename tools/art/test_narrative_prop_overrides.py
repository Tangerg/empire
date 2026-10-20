import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image, ImageDraw, ImageOps

from export_overrides import PACKS, export, fit_canvas


ROOT = Path(__file__).resolve().parents[2]
PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')
TOPICS = ['C01-PROP-LEFT-GLOVE', 'C01-PROP-EXILE-HARDTACK',
          'C01-PROP-IVRA-BROKEN-BRIDLE', 'C01-PROP-CROWN-FRAGMENT']


class NarrativePropTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.pack = self.root / PACK
        self.catalog = self.root / 'catalog.json'
        for campaign, directory, suffix in PACKS:
            relative = Path('packages') / campaign / 'assets' / directory
            pack = self.root / relative
            pack.mkdir(parents=True)
            name = f'manifest-final-{suffix}-v1.json'
            manifest = json.loads((ROOT / relative / name).read_text())
            (pack / name).write_text(json.dumps(manifest))
            for asset in manifest['assets']:
                target = pack / asset['png']
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(f'unchanged runtime {asset["topicId"]}'.encode())
        self.manifest = self.pack / 'manifest-final-fantasy-v1.json'
        self.hd_manifest = self.pack / 'manifest-tactical-runtime-hd.json'
        self.hd_manifest.write_text(json.dumps({'schemaVersion': 'candidate-01-runtime-hd-v2',
            'assetCount': 1, 'assets': [{'topicId': 'C01-UNIT-SWORDSMAN', 'width': 512,
                'height': 128, 'pixelDensity': 2, 'png': 'runtime-hd/combat-unit/swordsman.png'}]}))
        untouched_hd = self.pack / 'runtime-hd/combat-unit/swordsman.png'
        untouched_hd.parent.mkdir(parents=True)
        untouched_hd.write_bytes(b'unchanged unregistered HD runtime')
        self.source = self.pack / 'masters/narrative-test/prop.png'
        self.source.parent.mkdir(parents=True)
        self.make_source()
        self.override = {'kind': 'transparent-prop-v1', 'prop': self.ref()}
        self.register()

    def make_source(self, size=(512, 256)):
        image = Image.new('RGBA', size)
        draw = ImageDraw.Draw(image)
        draw.rectangle((32, 32, 112, 112), fill=(80, 90, 100, 255))
        draw.rectangle((48, 48, 72, 72), fill=(200, 50, 80, 128))
        draw.rectangle((80, 80, 104, 104), fill=(255, 0, 255, 255))
        draw.rectangle((160, 40, 240, 120), fill=(160, 140, 120, 255))
        draw.rectangle((184, 64, 216, 96), fill=(0, 0, 0, 0))
        draw.rectangle((400, 176, 440, 216), fill=(150, 60, 30, 255))
        image.save(self.source)

    def ref(self):
        return {'source': str(self.source.relative_to(self.root)),
                'sha256': hashlib.sha256(self.source.read_bytes()).hexdigest()}

    def register(self, entries=None):
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets':
            {TOPICS[0]: self.override} if entries is None else entries}))

    def snapshot(self):
        return {p: p.read_bytes() for p in self.root.rglob('*') if p.is_file()}

    def mutate_asset(self, topic=TOPICS[0], **patch):
        data = json.loads(self.manifest.read_text())
        next(a for a in data['assets'] if a['topicId'] == topic).update(patch)
        self.manifest.write_text(json.dumps(data))

    def register_hd(self, **patch):
        record = {'topicId': TOPICS[0], 'width': 256, 'height': 256, 'pixelDensity': 2,
                  'png': 'runtime-hd/narrative-static/prop-left-glove.png', **patch}
        self.hd_manifest.write_text(json.dumps({'schemaVersion': 'candidate-01-runtime-hd-v2',
                                               'assetCount': 1, 'assets': [record]}))

    def assert_rejected_unchanged(self, error):
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, error):
            export(self.root, self.catalog)
        self.assertEqual(self.snapshot(), before)

    def test_four_reviewed_ids_change_only_canonical_outputs_and_preserve_every_other_file(self):
        self.register({topic: self.override for topic in TOPICS})
        before = self.snapshot()
        report = export(self.root, self.catalog)
        targets = {self.root / r['output'] for r in report}
        self.assertEqual([r['topicId'] for r in report], TOPICS)
        self.assertEqual(len(targets), 4)
        self.assertEqual({p for p, raw in before.items() if p.read_bytes() != raw}, targets)
        self.assertEqual(set(self.snapshot()), set(before))
        assets = json.loads(self.manifest.read_text())['assets']
        for card in (a for a in assets if a.get('kind') == 'character-card'):
            self.assertEqual((self.pack / card['png']).read_bytes(), before[self.pack / card['png']])
        for result in report:
            self.assertEqual(result['size'], [128, 128])
            self.assertEqual(result['pixelDensity'], 1)
            self.assertEqual(result['kind'], 'transparent-prop-v1')
            self.assertEqual(result['sources']['prop'], {**self.ref(), 'size': [512, 256],
                'mode': 'RGBA', 'alpha': {'edgeMaxAlpha': 0, 'edgeNonzeroPixels': 0,
                                        'coreBoundingBox': [32, 32, 441, 217]}})
            self.assertEqual(result['propFit'], {'method': 'whole-canvas-contain',
                'sourceCropBox': None, 'renderSize': [124, 62], 'origin': [2, 33],
                'padding': 2, 'resampling': 'LANCZOS'})
            self.assertEqual(result['outputAlpha']['edgeMaxAlpha'], 0)
            self.assertEqual(result['outputAlpha']['edgeNonzeroPixels'], 0)
            asset = next(a for a in assets if a['topicId'] == result['topicId'])
            self.assertEqual(self.root / result['output'], self.pack / asset['png'])
            for field in ('anchor', 'frames', 'variants', 'frameWidth', 'frameHeight'):
                self.assertNotIn(field, asset)
            with Image.open(self.root / result['output']) as image:
                self.assertEqual(image.mode, 'RGBA')
                self.assertEqual(hashlib.sha256(image.tobytes()).hexdigest(), result['pixelSha256'])
        after = self.snapshot()
        self.assertEqual(export(self.root, self.catalog, check=True), report)
        self.assertEqual(export(self.root, self.catalog), report)
        self.assertEqual(self.snapshot(), after)

    def test_whole_canvas_retains_offcenter_subject_holes_detached_piece_and_rgba_samples(self):
        result = export(self.root, self.catalog)[0]
        with Image.open(self.source) as source, Image.open(self.root / result['output']) as output:
            expected = Image.new('RGBA', (128, 128))
            expected.paste(ImageOps.contain(source, (124, 124), Image.Resampling.LANCZOS), (2, 33))
            self.assertEqual(output.tobytes(), expected.tobytes())
            self.assertEqual(output.getpixel((16, 47)), (199, 49, 79, 128))
            self.assertEqual(output.getpixel((24, 55)), (255, 0, 255, 255))
            self.assertEqual(output.getpixel((50, 52))[3], 0)  # Interior ring hole.
            self.assertEqual(output.getpixel((78, 64))[3], 0)  # Gap between loose pieces.
            self.assertEqual(output.getpixel((103, 80)), (150, 60, 30, 255))
            self.assertEqual(output.getpixel((64, 90))[3], 0)  # Not bbox-recentered.

    def test_one_step_source_edge_is_retained_in_source_and_report_with_zero_output_edge(self):
        with Image.open(self.source) as source:
            source.putpixel((0, 64), (120, 60, 20, 1))
            source.save(self.source)
        self.override['prop'] = self.ref()
        self.register()
        before = self.source.read_bytes()
        report = export(self.root, self.catalog)[0]
        self.assertEqual(report['sources']['prop']['alpha']['edgeMaxAlpha'], 1)
        self.assertEqual(report['sources']['prop']['alpha']['edgeNonzeroPixels'], 1)
        self.assertEqual(report['outputAlpha']['edgeMaxAlpha'], 0)
        self.assertEqual(self.source.read_bytes(), before)

    def test_canvas_placement_retains_hidden_rgb_without_an_extra_source_over_operation(self):
        source = Image.new('RGBA', (128, 128))
        source.paste((80, 90, 100, 255), (32, 32, 96, 96))
        source.putpixel((20, 20), (5, 4, 3, 0))
        source.putpixel((21, 20), (150, 60, 30, 128))
        before = source.tobytes()
        rendered, geometry = fit_canvas(source, (132, 132), 2)
        self.assertEqual(geometry['renderSize'], [128, 128])
        self.assertEqual(rendered.crop((2, 2, 130, 130)).tobytes(), before)
        self.assertEqual(rendered.getpixel((22, 22)), (5, 4, 3, 0))
        self.assertEqual(rendered.getpixel((23, 22)), (150, 60, 30, 128))
        self.assertEqual(source.tobytes(), before)

    def test_optional_hd_is_direct_from_source_at_2x_and_never_synthesized(self):
        self.register_hd()
        before = self.snapshot()
        report = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in report], [[128, 128], [256, 256]])
        self.assertEqual([r['pixelDensity'] for r in report], [1, 2])
        self.assertEqual(report[1]['propFit']['padding'], 4)
        with Image.open(self.source) as source, Image.open(self.root / report[0]['output']) as canonical, \
                Image.open(self.root / report[1]['output']) as physical:
            expected = Image.new('RGBA', (256, 256))
            expected.paste(ImageOps.contain(source, (248, 248), Image.Resampling.LANCZOS), (4, 66))
            self.assertEqual(physical.tobytes(), expected.tobytes())
            self.assertNotEqual(physical.tobytes(), canonical.resize((256, 256), Image.Resampling.LANCZOS).tobytes())
        targets = {self.root / r['output'] for r in report}
        self.assertEqual({p for p, raw in before.items() if p.read_bytes() != raw}, targets & before.keys())
        self.assertEqual(set(self.snapshot()) - set(before), targets - before.keys())
        self.assertEqual(export(self.root, self.catalog, check=True), report)

    def test_source_must_cover_largest_requested_physical_size(self):
        for size, hd in [((127, 128), False), ((128, 127), False), ((255, 256), True), ((256, 255), True)]:
            with self.subTest(size=size, hd=hd):
                image = Image.new('RGBA', size)
                ImageDraw.Draw(image).rectangle((10, 10, 60, 60), fill=(30, 40, 50, 255))
                image.save(self.source)
                self.override['prop'] = self.ref()
                self.register()
                if hd:
                    self.register_hd()
                self.assert_rejected_unchanged('smaller than')

    def test_rejects_empty_rgb_opaque_matte_and_palette_sources(self):
        for mode, color in [('RGBA', (0, 0, 0, 0)), ('RGBA', (40, 50, 60, 255)),
                            ('RGBA', (40, 50, 60, 128)), ('RGB', (40, 50, 60)), ('P', 1)]:
            with self.subTest(mode=mode, color=color):
                Image.new(mode, (256, 256), color).save(self.source)
                self.override['prop'] = self.ref()
                self.register()
                self.assert_rejected_unchanged('RGBA|transparent exterior')

    def test_rejects_clipped_source_edges_without_cleanup(self):
        for alpha in (2, 8, 240, 255):
            with self.subTest(alpha=alpha), Image.open(self.source) as source:
                source.putpixel((0, 64), (120, 60, 20, alpha))
                source.save(self.source)
                self.override['prop'] = self.ref()
                self.register()
                self.assert_rejected_unchanged('outer edge alpha')

    def test_rejects_scene_and_card_kinds_and_implicit_source_schema(self):
        for topic in ('C01-CH01-S00', 'C01-CH03-S11', 'C01-CH01-S05', 'C01-CHAR-LEIA-01', 'unknown'):
            with self.subTest(topic=topic):
                self.register({topic: self.override})
                self.assert_rejected_unchanged('opaque-scene|character-card|unknown topic')
        for entry in (self.ref(), {**self.override, 'kind': 'unknown'},
                      {**self.override, 'sourceCropBox': None}, {**self.override, 'anchor': [64, 64]},
                      {'kind': 'transparent-prop-v1'}, {'kind': 'transparent-prop-v1', 'prop': None}):
            with self.subTest(entry=entry):
                self.register({TOPICS[0]: entry})
                self.assert_rejected_unchanged('transparent-prop-v1|source and sha256')

    def test_rejects_changed_geometry_and_even_empty_anchor_frame_metadata(self):
        original = self.manifest.read_bytes()
        for patch in ({'width': 129}, {'height': 128.0}, {'width': True}, {'anchor': None},
                      {'frames': 0}, {'variants': 0}, {'frameWidth': 128}, {'frameHeight': None}):
            with self.subTest(patch=patch):
                self.manifest.write_bytes(original)
                self.mutate_asset(**patch)
                self.assert_rejected_unchanged('unframed, unanchored key-prop')
        self.manifest.write_bytes(original)
        self.mutate_asset(kind='unrecognized')
        self.assert_rejected_unchanged('unsupported narrative kind')

    def test_other_campaign_is_outside_bounded_prop_contract(self):
        campaign, directory, suffix = PACKS[1]
        manifest = self.root / 'packages' / campaign / 'assets' / directory / f'manifest-final-{suffix}-v1.json'
        asset = next(a for a in json.loads(self.manifest.read_text())['assets'] if a['topicId'] == TOPICS[0])
        asset['topicId'] = 'C02-test-prop'
        manifest.write_text(json.dumps({'assets': [asset]}))
        self.register({asset['topicId']: self.override})
        self.assert_rejected_unchanged('Gray Flag')

    def test_rejects_malformed_or_non_2x_hd(self):
        for patch in ({'width': 128}, {'height': 256.0}, {'width': True}, {'pixelDensity': True},
                      {'pixelDensity': 1}, {'pixelDensity': 3}, {'pixelDensity': float('nan')},
                      {'pixelDensity': float('inf')}, {'pixelDensity': '2'}, {'anchor': None}):
            with self.subTest(patch=patch):
                self.register_hd(**patch)
                self.assert_rejected_unchanged('key-prop HD')
        self.hd_manifest.write_text('{"schemaVersion":"old", "assets":[]}')
        self.assert_rejected_unchanged('HD contract requires v2')

    def test_invalid_later_source_prevents_all_earlier_writes(self):
        bad = copy.deepcopy(self.override)
        bad['prop']['sha256'] = '0' * 64
        self.register({TOPICS[0]: self.override, TOPICS[1]: bad})
        self.assert_rejected_unchanged('SHA-256 mismatch')

    def test_source_paths_hash_and_png_format_are_enforced(self):
        for path, error in [('../outside.png', 'escapes'), ('/absolute.png', 'relative path'),
                            ('packages/story-candidate-02/assets/final-scifi-v1/masters/prop.png', 'pack masters'),
                            (str(PACK / 'runtime/narrative-static/legacy.png'), 'pack masters')]:
            with self.subTest(path=path):
                self.override['prop'] = {**self.ref(), 'source': path}
                self.register()
                self.assert_rejected_unchanged(error)
        outside = self.root / 'outside.png'
        outside.write_bytes(self.source.read_bytes())
        linked = self.source.with_name('linked.png')
        linked.symlink_to(outside)
        self.override['prop'] = {**self.ref(), 'source': str(linked.relative_to(self.root))}
        self.register()
        self.assert_rejected_unchanged('pack masters')
        Image.new('RGB', (256, 256)).save(self.source, format='JPEG')
        self.override['prop'] = self.ref()
        self.register()
        self.assert_rejected_unchanged('source must be PNG')

    def test_source_reference_rejects_extra_fields(self):
        self.override['prop'] = {**self.ref(), 'crop': [0, 0, 128, 128]}
        self.register()
        self.assert_rejected_unchanged('source and sha256 only')

    def test_duplicate_keys_and_destinations_are_rejected_before_writes(self):
        self.catalog.write_text('{"schemaVersion":1,"assets":{},"assets":{}}')
        self.assert_rejected_unchanged('duplicate JSON key')
        assets = json.loads(self.manifest.read_text())['assets']
        target = next(a['png'] for a in assets if a['topicId'] == TOPICS[0])
        self.mutate_asset(TOPICS[1], png=target)
        self.register({TOPICS[0]: self.override, TOPICS[1]: self.override})
        self.assert_rejected_unchanged('duplicate destination')

    def test_canonical_and_hd_paths_cannot_escape_the_owned_runtime_directory(self):
        original = self.manifest.read_bytes()
        for path in ('runtime/battle-prop/prop.png', 'runtime/narrative-static/prop.jpg',
                     'runtime/narrative-static/child/prop.png', '../prop.png'):
            with self.subTest(path=path):
                self.manifest.write_bytes(original)
                self.mutate_asset(png=path)
                self.assert_rejected_unchanged('unexpected export path|escapes')
        self.manifest.write_bytes(original)
        self.register_hd(png='runtime/narrative-static/prop.png')
        self.assert_rejected_unchanged('unexpected export path')

    def test_check_detects_stale_pixels_without_repairing(self):
        report = export(self.root, self.catalog)[0]
        Image.new('RGBA', (128, 128)).save(self.root / report['output'])
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, 'stale export'):
            export(self.root, self.catalog, check=True)
        self.assertEqual(self.snapshot(), before)


if __name__ == '__main__':
    unittest.main()
