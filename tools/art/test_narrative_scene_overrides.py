import copy
import csv
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image, ImageDraw

from export_overrides import PACKS, export


ROOT = Path(__file__).resolve().parents[2]
PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')
TOPICS = ['C01-CH01-S05', 'C01-CH01-S01', 'C01-ARCH-THREE-BRIDGES', 'C01-ARCH-REDSTONE']
SQUARE_TOPIC = 'C01-CH03-S11'


class NarrativeSceneTests(unittest.TestCase):
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
        self.hd_manifest = self.pack / 'manifest-tactical-runtime-hd.json'
        self.source = self.pack / 'masters/narrative-test/scene.png'
        self.source.parent.mkdir(parents=True)
        self.make_source()
        self.override = {'kind': 'opaque-scene-v1', 'scene': self.ref(), 'sourceCropBox': None}
        self.register()

    def make_source(self, size=(768, 432), mode='RGB'):
        image = Image.new(mode, size, (35, 60, 85, 255) if mode == 'RGBA' else (35, 60, 85))
        draw = ImageDraw.Draw(image)
        for x in range(0, size[0], 7):
            draw.line((x, 0, x, size[1] - 1), fill=(220, 130, 40))
        draw.rectangle((0, 0, 24, 24), fill=(240, 0, 0))
        draw.rectangle((size[0] - 25, size[1] - 25, size[0] - 1, size[1] - 1), fill=(0, 0, 240))
        image.save(self.source)

    def ref(self):
        return {'source': str(self.source.relative_to(self.root)),
                'sha256': hashlib.sha256(self.source.read_bytes()).hexdigest()}

    def register(self, entries=None):
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets':
                                           {TOPICS[0]: self.override} if entries is None else entries}))

    def outputs(self):
        return {a['topicId']: (self.pack / a['png']).read_bytes() for a in self.assets}

    def mutate_asset(self, topic=TOPICS[0], **values):
        data = json.loads(self.manifest.read_text())
        next(a for a in data['assets'] if a['topicId'] == topic).update(values)
        self.manifest.write_text(json.dumps(data))

    def register_hd(self, **values):
        record = {'topicId': TOPICS[0], 'png': 'runtime-hd/narrative-static/ch01-s05.png',
                  'width': 512, 'height': 288, 'pixelDensity': 2, **values}
        self.hd_manifest.write_text(json.dumps({'schemaVersion': 'candidate-01-runtime-hd-v2', 'assetCount': 1, 'assets': [record]}))

    def test_whole_canvas_opaque_export_provenance_and_untouched_unregistered_assets(self):
        originals = self.outputs()
        source_bytes = self.source.read_bytes()
        self.register({topic: self.override for topic in TOPICS})
        manifests = {p: p.read_bytes() for p in self.root.rglob('*.json')}
        report = export(self.root, self.catalog)
        self.assertEqual([r['topicId'] for r in report], TOPICS)
        self.assertEqual({t for t, b in originals.items() if self.outputs()[t] != b}, set(TOPICS))
        self.assertEqual(self.source.read_bytes(), source_bytes)
        self.assertEqual(manifests, {p: p.read_bytes() for p in manifests})
        self.assertFalse(self.hd_manifest.exists())
        self.assertFalse((self.pack / 'runtime-hd').exists())
        for result in report:
            self.assertEqual(result['size'], [256, 144])
            self.assertEqual(result['pixelDensity'], 1)
            self.assertEqual(result['sources']['scene'], {**self.ref(), 'size': [768, 432],
                                                        'mode': 'RGB', 'alphaExtrema': [255, 255]})
            self.assertEqual(result['sceneFit'], {'method': 'explicit-region-resize', 'resampling': 'LANCZOS',
                                                'sourceCropBox': None, 'sourceRegion': [0, 0, 768, 432],
                                                'croppedSourcePixels': {'left': 0, 'top': 0, 'right': 0, 'bottom': 0},
                                                'retainedAreaFraction': 1})
            self.assertIn('actual UI cover crops', result['visualReviewRequired'])
            with Image.open(self.root / result['output']) as image, Image.open(self.source) as master:
                self.assertEqual(image.mode, 'RGBA')
                self.assertEqual(image.getchannel('A').getextrema(), (255, 255))
                self.assertEqual(image.tobytes(), master.convert('RGBA').resize((256, 144), Image.Resampling.LANCZOS).tobytes())
                self.assertEqual(image.getpixel((0, 0)), (240, 0, 0, 255))
                self.assertEqual(image.getpixel((255, 143)), (0, 0, 240, 255))
                self.assertEqual(result['pixelSha256'], hashlib.sha256(image.tobytes()).hexdigest())
        after = self.outputs()
        self.assertEqual(export(self.root, self.catalog, check=True), report)
        self.assertEqual(export(self.root, self.catalog), report)
        self.assertEqual(self.outputs(), after)

    def test_registered_hd_is_independent_source_sampling_and_keeps_logical_manifest(self):
        self.register_hd()
        manifests = {p: p.read_bytes() for p in self.root.rglob('*.json')}
        report = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in report], [[256, 144], [512, 288]])
        self.assertEqual([r['pixelDensity'] for r in report], [1, 2])
        with Image.open(self.source) as source, Image.open(self.root / report[0]['output']) as native, \
                Image.open(self.root / report[1]['output']) as hd:
            self.assertEqual(hd.tobytes(), source.convert('RGBA').resize((512, 288), Image.Resampling.LANCZOS).tobytes())
            self.assertNotEqual(hd.tobytes(), native.resize((512, 288), Image.Resampling.LANCZOS).tobytes())
        self.assertEqual(manifests, {p: p.read_bytes() for p in manifests})
        self.assertEqual(export(self.root, self.catalog, check=True), report)

    def test_registered_cemetery_preserves_logical_identity_and_exact_source_pixels(self):
        actual = json.loads((ROOT / PACK / 'manifest-final-fantasy-v1.json').read_text())
        asset = next(a for a in actual['assets'] if a['topicId'] == SQUARE_TOPIC)
        self.assertEqual(asset, {
            'assetId': 'C01-FFV1-CH03-S11', 'topicId': SQUARE_TOPIC, 'label': '墓园姓名木片',
            'category': 'narrative-static', 'png': 'runtime/narrative-static/ch03-s11.png',
            'width': 128, 'height': 128, 'runtimeReady': True,
            'qualityTier': 'approved-chunky-v5-export-verified', 'kind': 'scene',
        })
        runtime = ROOT / PACK / asset['png']
        override = json.loads((ROOT / 'tools/art/source-overrides.json').read_text())['assets'][SQUARE_TOPIC]
        source = ROOT / override['scene']['source']
        self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), override['scene']['sha256'])
        with Image.open(source) as master, Image.open(runtime) as rendered:
            self.assertEqual(rendered.tobytes(), master.convert('RGBA').resize((128,128), Image.Resampling.LANCZOS).tobytes())
        with Image.open(runtime) as image:
            self.assertEqual(image.size, (128, 128))
            self.assertEqual(image.getchannel('A').getextrema(), (255, 255))
        self.assertEqual(len([a for a in self.assets if a['kind'] == 'scene'
                              and (a['width'], a['height']) == (256, 144)]), 41)
        for topic in ('C01-PROP-LEFT-GLOVE', 'C01-PROP-NAME-TAGS'):
            self.assertEqual(next(a for a in self.assets if a['topicId'] == topic)['kind'], 'key-prop')
        hd = json.loads((ROOT / PACK / 'manifest-tactical-runtime-hd.json').read_text())
        physical = next(a for a in hd['assets'] if a['topicId'] == SQUARE_TOPIC)
        self.assertEqual((physical['width'],physical['height'],physical['pixelDensity']), (1254,1254,1254/128))
        with Image.open(ROOT / PACK / physical['png']) as rendered, Image.open(source) as master:
            self.assertEqual(rendered.tobytes(), master.convert('RGBA').tobytes())

    def test_opening_scene_registers_complete_reviewed_square_and_preserves_inventory_history(self):
        topic = 'C01-CH01-S00'
        asset = next(a for a in self.assets if a['topicId'] == topic)
        self.assertEqual(asset, {
            'assetId': 'C01-FFV1-CH01-S00', 'topicId': topic, 'label': '罗德里克系护腕',
            'category': 'narrative-static', 'png': 'runtime/narrative-static/ch01-s00.png',
            'width': 128, 'height': 128, 'runtimeReady': True,
            'qualityTier': 'approved-chunky-v5-export-verified', 'kind': 'scene',
        })
        entry = json.loads((ROOT / 'tools/art/source-overrides.json').read_text())['assets'][topic]
        source = ROOT / entry['scene']['source']
        self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), entry['scene']['sha256'])
        self.assertEqual(entry['scene']['sha256'], 'e84b6873a0237d5a01c4a111795dd053a62eafd9a90bfb98b130c9379f98c3d9')
        hd = json.loads((ROOT / PACK / 'manifest-tactical-runtime-hd.json').read_text())
        physical = next(a for a in hd['assets'] if a['topicId'] == topic)
        self.assertEqual((physical['width'], physical['height'], physical['pixelDensity']), (1254,1254,1254/128))
        with Image.open(source) as master, Image.open(ROOT / PACK / asset['png']) as image, Image.open(ROOT / PACK / physical['png']) as native:
            expected = master.convert('RGBA')
            self.assertEqual(native.tobytes(), expected.tobytes())
            self.assertEqual(image.tobytes(), expected.resize((128,128), Image.Resampling.LANCZOS).tobytes())
            self.assertEqual(image.getchannel('A').getextrema(), (255,255))
        inventory = json.loads((ROOT / 'docs/art-rebuild-20261019/canonical-topic-manifest.json').read_text())
        row = next(a for a in inventory if a['topicId'] == topic)
        self.assertEqual(row['kind'], 'scene')
        self.assertEqual(row['alpha_requirement'], 'opaque-scene')
        self.assertEqual(row['status'], 'pending-redraw')
        with (ROOT / 'docs/art-rebuild-20261019/canonical-topic-manifest.csv').open() as handle:
            csv_row = next(a for a in csv.DictReader(handle) if a['topicId'] == topic)
        for field in ('kind', 'alpha_requirement', 'status', 'path'):
            self.assertEqual(csv_row[field], row[field])
        self.assertEqual({key: row[key] for key in asset if key != 'qualityTier'}, {key: value for key,value in asset.items() if key != 'qualityTier'})
        self.assertEqual(row['qualityTier'], 'final-cartoon-fantasy-v1')
        self.assertEqual(len([a for a in self.assets if a['kind'] == 'scene']), 43)
        self.assertEqual(len([a for a in self.assets if a['kind'] == 'key-prop']), 15)
        self.assertEqual(next(a for a in self.assets if a['topicId'] == 'C01-PROP-LEFT-GLOVE')['kind'], 'key-prop')

    def test_opening_scene_uses_square_opaque_contract_and_rejects_transparent_source(self):
        topic = 'C01-CH01-S00'
        self.make_source((617, 617))
        self.override['scene'] = self.ref()
        self.register({topic: self.override})
        self.register_hd(topicId=topic, png='runtime-hd/narrative-static/ch01-s00.png',
                         width=617, height=617, pixelDensity=617 / 128)
        originals = self.outputs()
        manifests = {p: p.read_bytes() for p in self.root.rglob('*.json')}
        report = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in report], [[128, 128], [617, 617]])
        self.assertEqual({t for t, b in originals.items() if self.outputs()[t] != b}, {topic})
        with Image.open(self.source) as source:
            for result in report:
                with Image.open(self.root / result['output']) as image:
                    expected = source.convert('RGBA').resize(tuple(result['size']), Image.Resampling.LANCZOS)
                    self.assertEqual(image.tobytes(), expected.tobytes())
                    self.assertEqual(image.getchannel('A').getextrema(), (255, 255))
        self.assertEqual(export(self.root, self.catalog, check=True), report)
        self.assertEqual(manifests, {p: p.read_bytes() for p in manifests})
        before = {self.root / r['output']: (self.root / r['output']).read_bytes() for r in report}
        canonical = self.outputs()
        self.make_source((617, 617), mode='RGBA')
        with Image.open(self.source) as source:
            source.putpixel((0, 0), (0, 0, 0, 0))
            source.save(self.source)
        self.override['scene'] = self.ref()
        self.register({topic: self.override})
        with self.assertRaisesRegex(ValueError, 'complete scene master'):
            export(self.root, self.catalog)
        self.assertEqual(self.outputs(), canonical)
        self.assertEqual(before, {p: p.read_bytes() for p in before})

    def test_square_whole_source_and_explicit_native_density_keep_canonical_identity(self):
        self.make_source((617, 617))
        source_bytes = self.source.read_bytes()
        self.override['scene'] = self.ref()
        self.register({SQUARE_TOPIC: self.override})
        originals = self.outputs()
        canonical = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in canonical], [[128, 128]])
        self.assertFalse(self.hd_manifest.exists())
        self.assertEqual({t for t, b in originals.items() if self.outputs()[t] != b}, {SQUARE_TOPIC})
        self.register_hd(topicId=SQUARE_TOPIC, png='runtime-hd/narrative-static/ch03-s11.png',
                         width=617, height=617, pixelDensity=617 / 128)
        manifests = {p: p.read_bytes() for p in self.root.rglob('*.json')}
        report = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in report], [[128, 128], [617, 617]])
        self.assertEqual([r['pixelDensity'] for r in report], [1, 617 / 128])
        self.assertEqual(report[0], canonical[0])
        self.assertEqual(report[1]['sceneFit']['sourceRegion'], [0, 0, 617, 617])
        self.assertIsNone(report[1]['sceneFit']['sourceCropBox'])
        self.assertEqual(report[1]['sceneFit']['retainedAreaFraction'], 1)
        self.assertEqual(report[1]['sceneFit']['method'], 'explicit-region-copy')
        self.assertIsNone(report[1]['sceneFit']['resampling'])
        with Image.open(self.source) as source:
            for result in report:
                with Image.open(self.root / result['output']) as image:
                    expected = source.convert('RGBA').resize(tuple(result['size']), Image.Resampling.LANCZOS)
                    self.assertEqual(image.tobytes(), expected.tobytes())
                    self.assertEqual(image.getchannel('A').getextrema(), (255, 255))
        self.assertEqual(self.source.read_bytes(), source_bytes)
        self.assertEqual(manifests, {p: p.read_bytes() for p in manifests})
        self.assertEqual(export(self.root, self.catalog, check=True), report)
        self.assertEqual(export(self.root, self.catalog), report)

    def test_square_scene_uses_only_explicit_square_crop(self):
        self.make_source((768, 600), mode='RGBA')
        source_bytes = self.source.read_bytes()
        self.override.update(scene=self.ref(), sourceCropBox=[40, 20, 552, 532])
        self.register({SQUARE_TOPIC: self.override})
        self.register_hd(topicId=SQUARE_TOPIC, png='runtime-hd/narrative-static/ch03-s11.png',
                         width=512, height=512, pixelDensity=4)
        for result in export(self.root, self.catalog):
            self.assertEqual(result['sceneFit']['sourceRegion'], [40, 20, 552, 532])
            self.assertEqual(result['sceneFit']['croppedSourcePixels'],
                             {'left': 40, 'top': 20, 'right': 216, 'bottom': 68})
            with Image.open(self.source) as source, Image.open(self.root / result['output']) as image:
                expected = source.crop((40, 20, 552, 532)).resize(tuple(result['size']), Image.Resampling.LANCZOS)
                self.assertEqual(image.tobytes(), expected.tobytes())
        self.assertEqual(self.source.read_bytes(), source_bytes)

    def test_square_scene_rejects_wide_source_or_crop_without_writes(self):
        before = self.outputs()
        for crop in (None, [0, 0, 512, 288], [0, 0, 400, 399]):
            with self.subTest(crop=crop):
                self.override['sourceCropBox'] = crop
                self.register({SQUARE_TOPIC: self.override})
                with self.assertRaisesRegex(ValueError, 'canonical aspect exactly 1:1'):
                    export(self.root, self.catalog)
                self.assertEqual(self.outputs(), before)

    def test_square_scene_rejects_transparency_even_outside_reviewed_crop(self):
        self.make_source((512, 512), mode='RGBA')
        with Image.open(self.source) as source:
            source.putpixel((0, 0), (10, 20, 30, 254))
            source.save(self.source)
        self.override.update(scene=self.ref(), sourceCropBox=[64, 64, 448, 448])
        self.register({SQUARE_TOPIC: self.override})
        before = self.outputs()
        with self.assertRaisesRegex(ValueError, 'complete scene master'):
            export(self.root, self.catalog)
        self.assertEqual(self.outputs(), before)

    def test_square_source_minimum_follows_canonical_geometry_and_density(self):
        self.make_source((128, 128))
        self.override['scene'] = self.ref()
        self.register({SQUARE_TOPIC: self.override})
        self.assertEqual(export(self.root, self.catalog)[0]['sceneFit']['method'], 'explicit-region-copy')
        before = self.outputs()
        self.make_source((64, 64))
        self.override['scene'] = self.ref()
        self.register({SQUARE_TOPIC: self.override})
        with self.assertRaisesRegex(ValueError, 'smaller than'):
            export(self.root, self.catalog)
        self.make_source((128, 128))
        self.override['scene'] = self.ref()
        self.register({SQUARE_TOPIC: self.override})
        self.register_hd(topicId=SQUARE_TOPIC, png='runtime-hd/narrative-static/ch03-s11.png',
                         width=256, height=256, pixelDensity=2)
        with self.assertRaisesRegex(ValueError, 'smaller than'):
            export(self.root, self.catalog)
        self.assertEqual(self.outputs(), before)
        self.assertFalse((self.pack / 'runtime-hd').exists())

    def test_square_unknown_geometry_or_framing_cannot_be_inferred_from_source(self):
        self.make_source((512, 512))
        self.override['scene'] = self.ref()
        self.register({SQUARE_TOPIC: self.override})
        manifest = self.manifest.read_bytes()
        before = self.outputs()
        for patch in ({'width': 256, 'height': 256}, {'height': 127}, {'width': 128.0},
                      {'width': True}, {'height': None}, {'frames': 0}, {'anchor': None},
                      {'variants': 0}, {'frameWidth': 128}, {'frameHeight': 128}):
            with self.subTest(patch=patch):
                self.manifest.write_bytes(manifest)
                self.mutate_asset(SQUARE_TOPIC, **patch)
                with self.assertRaisesRegex(ValueError, 'unframed scene'):
                    export(self.root, self.catalog)
                self.assertEqual(self.outputs(), before)

    def test_square_hd_requires_exact_declared_ratio_and_density(self):
        self.make_source((512, 512))
        self.override['scene'] = self.ref()
        self.register({SQUARE_TOPIC: self.override})
        before = self.outputs()
        for patch in ({'width': 512, 'height': 288}, {'height': 255}, {'pixelDensity': 2.5},
                      {'width': None}, {'height': 256.0}, {'pixelDensity': True}):
            with self.subTest(patch=patch):
                self.register_hd(**{'topicId': SQUARE_TOPIC,
                                    'png': 'runtime-hd/narrative-static/ch03-s11.png',
                                    'width': 256, 'height': 256, 'pixelDensity': 2, **patch})
                with self.assertRaisesRegex(ValueError, 'HD contract'):
                    export(self.root, self.catalog)
                self.assertEqual(self.outputs(), before)
                self.assertFalse((self.pack / 'runtime-hd').exists())

    def test_explicit_reviewed_crop_keeps_complete_source_and_reports_removed_area(self):
        self.make_source((768, 600), mode='RGBA')
        original = self.source.read_bytes()
        self.override.update(scene=self.ref(), sourceCropBox=[0, 72, 768, 504])
        self.register()
        self.register_hd()
        for result in export(self.root, self.catalog):
            self.assertEqual(result['sceneFit']['sourceCropBox'], [0, 72, 768, 504])
            self.assertEqual(result['sceneFit']['sourceRegion'], [0, 72, 768, 504])
            self.assertEqual(result['sceneFit']['retainedAreaFraction'], 432 / 600)
            self.assertEqual(result['sources']['scene']['size'], [768, 600])
            with Image.open(self.source) as source, Image.open(self.root / result['output']) as image:
                expected = source.crop((0, 72, 768, 504)).resize(tuple(result['size']), Image.Resampling.LANCZOS)
                self.assertEqual(image.tobytes(), expected.tobytes())
        self.assertEqual(self.source.read_bytes(), original)

    def test_near_16_by_9_master_requires_declared_crop_and_reports_every_removed_pixel(self):
        self.make_source((1672, 941))
        original = self.source.read_bytes()
        self.override['scene'] = self.ref()
        self.register()
        with self.assertRaisesRegex(ValueError, '16:9'):
            export(self.root, self.catalog)
        self.override['sourceCropBox'] = [4, 2, 1668, 938]
        self.register()
        self.register_hd()
        for result in export(self.root, self.catalog):
            self.assertEqual(result['sources']['scene']['size'], [1672, 941])
            self.assertEqual(result['sceneFit']['croppedSourcePixels'], {'left': 4, 'top': 2, 'right': 4, 'bottom': 3})
            self.assertEqual(result['sceneFit']['retainedAreaFraction'], 1664 * 936 / (1672 * 941))
            with Image.open(self.source) as source, Image.open(self.root / result['output']) as image:
                expected = source.crop((4, 2, 1668, 938)).convert('RGBA').resize(tuple(result['size']), Image.Resampling.LANCZOS)
                self.assertEqual(image.tobytes(), expected.tobytes())
        self.assertEqual(self.source.read_bytes(), original)

    def test_non_16_by_9_master_never_silently_center_crops_or_stretches(self):
        self.make_source((768, 768))
        self.override['scene'] = self.ref()
        self.register()
        before = self.outputs()
        with self.assertRaisesRegex(ValueError, '16:9'):
            export(self.root, self.catalog)
        self.assertEqual(self.outputs(), before)

    def test_crop_must_be_explicit_valid_integer_region_and_16_by_9(self):
        for crop in [[0, 0, 768, 400], [0, 0, 768, 0], [768, 0, 0, 432], [-16, 0, 752, 432],
                     [0, 0, 784, 441], [False, 0, 768, 432], [0.0, 0, 768, 432],
                     [0, 0, float('inf'), 432], [0, 0, 768], 'center-cover']:
            with self.subTest(crop=crop):
                self.override['sourceCropBox'] = crop
                self.register()
                with self.assertRaisesRegex(ValueError, 'sourceCropBox|16:9'):
                    export(self.root, self.catalog)

    def test_schema_rejects_implicit_framing_or_transparent_asset_contracts(self):
        for spec in [self.ref(), {'kind': 'opaque-scene-v1', 'scene': self.ref()},
                     {**self.override, 'kind': 'character-card-composite-v1'},
                     {**self.override, 'padding': 2}]:
            with self.subTest(spec=spec):
                self.register({TOPICS[0]: spec})
                with self.assertRaisesRegex(ValueError, 'opaque-scene-v1'):
                    export(self.root, self.catalog)

    def test_only_rgb_or_completely_opaque_rgba_masters_are_allowed(self):
        for mode in ['P', 'L', 'RGBA']:
            with self.subTest(mode=mode):
                Image.new(mode, (768, 432)).save(self.source)
                self.override['scene'] = self.ref()
                self.register()
                with self.assertRaisesRegex(ValueError, 'fully opaque'):
                    export(self.root, self.catalog)
        self.make_source((768, 600), mode='RGBA')
        with Image.open(self.source) as source:
            source.putpixel((0, 0), (10, 20, 30, 254))
            source.save(self.source)
        self.override.update(scene=self.ref(), sourceCropBox=[0, 72, 768, 504])
        self.register()
        with self.assertRaisesRegex(ValueError, 'complete scene master'):
            export(self.root, self.catalog)

    def test_small_source_cannot_be_upscaled_into_native_or_hd_claim(self):
        for size, hd in [((128, 72), False), ((256, 144), True)]:
            with self.subTest(size=size, hd=hd):
                self.make_source(size)
                self.override['scene'] = self.ref()
                self.register()
                if hd:
                    self.register_hd()
                with self.assertRaisesRegex(ValueError, 'smaller than'):
                    export(self.root, self.catalog)

    def test_existing_characters_props_and_other_campaigns_rejected(self):
        for topic in ['unknown', 'C01-CHAR-LEIA-01', 'C01-PROP-LEFT-GLOVE', 'C01-PROP-NAME-TAGS']:
            with self.subTest(topic=topic):
                self.register({topic: self.override})
                with self.assertRaises(ValueError):
                    export(self.root, self.catalog)
        campaign, directory, suffix = PACKS[1]
        pack = self.root / 'packages' / campaign / 'assets' / directory
        other = {**next(a for a in self.assets if a['topicId'] == TOPICS[0]), 'topicId': 'C02-test-scene'}
        (pack / f'manifest-final-{suffix}-v1.json').write_text(json.dumps({'assets': [other]}))
        self.register({other['topicId']: self.override})
        with self.assertRaisesRegex(ValueError, 'Gray Flag'):
            export(self.root, self.catalog)

    def test_wrong_native_geometry_frames_or_anchor_rejected(self):
        original = self.manifest.read_bytes()
        for mutation in [{'width': 512}, {'height': 143}, {'frames': 1}, {'frames': 0},
                         {'variants': 1}, {'frameWidth': 256}, {'frameHeight': 144}, {'anchor': [128, 144]}]:
            with self.subTest(mutation=mutation):
                self.manifest.write_bytes(original)
                self.mutate_asset(**mutation)
                with self.assertRaisesRegex(ValueError, 'unframed scene'):
                    export(self.root, self.catalog)

    def test_native_fractional_density_copies_reviewed_region_without_resampling(self):
        self.make_source((1672, 941))
        source_bytes = self.source.read_bytes()
        self.override.update(scene=self.ref(), sourceCropBox=[4, 2, 1668, 938])
        self.register()
        canonical = export(self.root, self.catalog)[0]
        canonical_bytes = (self.root / canonical['output']).read_bytes()
        self.register_hd(width=1664, height=936, pixelDensity=6.5)
        manifests = {p: p.read_bytes() for p in self.root.rglob('*.json')}
        report = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in report], [[256, 144], [1664, 936]])
        self.assertEqual([r['pixelDensity'] for r in report], [1, 6.5])
        self.assertEqual(report[1]['sceneFit']['method'], 'explicit-region-copy')
        self.assertIsNone(report[1]['sceneFit']['resampling'])
        self.assertEqual(report[0]['sceneFit']['resampling'], 'LANCZOS')
        self.assertEqual(canonical_bytes, (self.root / report[0]['output']).read_bytes())
        with Image.open(self.source) as source, Image.open(self.root / report[1]['output']) as physical:
            self.assertEqual(physical.tobytes(), source.crop((4, 2, 1668, 938)).convert('RGBA').tobytes())
            self.assertEqual(physical.getchannel('A').getextrema(), (255, 255))
        self.assertEqual(self.source.read_bytes(), source_bytes)
        self.assertEqual(manifests, {p: p.read_bytes() for p in manifests})
        self.assertEqual(export(self.root, self.catalog, check=True), report)
        self.assertEqual(export(self.root, self.catalog), report)

    def test_density_cannot_request_more_pixels_than_reviewed_source_region(self):
        self.make_source((1672, 941))
        self.override.update(scene=self.ref(), sourceCropBox=[4, 2, 1668, 938])
        self.register()
        self.register_hd(width=1792, height=1008, pixelDensity=7)
        before = self.outputs()
        with self.assertRaisesRegex(ValueError, 'smaller than'):
            export(self.root, self.catalog)
        self.assertEqual(self.outputs(), before)
        self.assertFalse((self.pack / 'runtime-hd').exists())

    def test_wrong_hd_dimensions_or_invalid_density_rejected(self):
        mutations = [{'width': 1024}, {'height': 144}, {'width': 511.5}, {'height': 0},
                     {'width': True}, {'height': False}, {'width': -512}, {'height': float('inf')}]
        mutations += [{'pixelDensity': value} for value in
                      [None, '6.5', True, False, 0, -2, 1, 1.5, float('nan'), float('inf'), -float('inf')]]
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                self.register_hd(**mutation)
                before = self.outputs()
                with self.assertRaisesRegex(ValueError, 'HD contract'):
                    export(self.root, self.catalog)
                self.assertEqual(self.outputs(), before)

    def test_legacy_schema_global_density_and_wrong_count_rejected(self):
        for mutation in [{'schemaVersion': 'candidate-01-tactical-runtime-hd-v1'},
                         {'pixelDensity': 2}, {'pixelDensity': 6.5}, {'assetCount': 0}]:
            with self.subTest(mutation=mutation):
                self.register_hd()
                data = json.loads(self.hd_manifest.read_text())
                data.update(mutation)
                self.hd_manifest.write_text(json.dumps(data))
                with self.assertRaisesRegex(ValueError, 'HD contract'):
                    export(self.root, self.catalog)

    def test_hash_mismatch_and_cross_pack_or_escaped_source_fail_before_writes(self):
        before = self.outputs()
        for reference in [{**self.ref(), 'sha256': '0' * 64},
                          {**self.ref(), 'source': 'packages/story-candidate-02/assets/final-scifi-v1/masters/scene.png'},
                          {**self.ref(), 'source': '../outside.png'}]:
            with self.subTest(reference=reference):
                self.override['scene'] = reference
                self.register()
                with self.assertRaisesRegex(ValueError, 'SHA-256|pack masters|escapes'):
                    export(self.root, self.catalog)
                self.assertEqual(self.outputs(), before)

    def test_later_invalid_scene_prevents_all_registered_writes(self):
        before = self.outputs()
        invalid = copy.deepcopy(self.override)
        invalid['sourceCropBox'] = [0, 0, 100, 100]
        self.register({TOPICS[0]: self.override, TOPICS[1]: invalid})
        with self.assertRaisesRegex(ValueError, '16:9'):
            export(self.root, self.catalog)
        self.assertEqual(self.outputs(), before)

    def test_native_hd_wrong_directories_and_duplicate_targets_rejected(self):
        self.mutate_asset(png='runtime/battle-prop/wrong.png')
        with self.assertRaisesRegex(ValueError, 'unexpected export path'):
            export(self.root, self.catalog)
        native_path = next(a['png'] for a in self.assets if a['topicId'] == TOPICS[0])
        self.mutate_asset(png=native_path)
        self.register_hd(png='runtime-hd/battle-prop/wrong.png')
        with self.assertRaisesRegex(ValueError, 'unexpected export path'):
            export(self.root, self.catalog)
        self.hd_manifest.unlink()
        self.mutate_asset(TOPICS[1], png=native_path)
        self.register({topic: self.override for topic in TOPICS[:2]})
        with self.assertRaisesRegex(ValueError, 'duplicate destination'):
            export(self.root, self.catalog)

    def test_check_reports_stale_export_without_repairing_it(self):
        self.register_hd()
        report = export(self.root, self.catalog)
        target = self.root / report[1]['output']
        Image.new('RGBA', (512, 288), (1, 2, 3, 255)).save(target)
        before = {p: p.read_bytes() for p in self.root.rglob('*.png')}
        with self.assertRaisesRegex(ValueError, 'stale export'):
            export(self.root, self.catalog, check=True)
        self.assertEqual(before, {p: p.read_bytes() for p in before})


if __name__ == '__main__':
    unittest.main()
