import copy
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from export_overrides import PACKS, validate_alpha
from export_fx_overrides import AMBIENT_PHASE_ORDERS, PHASE_ORDERS, export, validate_fx_alpha


class FxTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        for index, (campaign, directory, suffix) in enumerate(PACKS):
            pack = self.root / 'packages' / campaign / 'assets' / directory
            pack.mkdir(parents=True)
            manifest = pack / f'manifest-final-{suffix}-v1.json'
            manifest.write_text('{"assets":[]}')
            if index == 0:
                self.pack, self.manifest = pack, manifest
        self.asset = {'topicId': 'C01-FX-01', 'category': 'fx', 'width': 128, 'height': 32,
                      'frameWidth': 32, 'frameHeight': 32, 'frames': 4, 'fps': 12, 'loop': False,
                      'blendMode': 'normal', 'anchor': [16, 16], 'png': 'runtime/fx/fx-01.png'}
        self.hd = {'topicId': 'C01-FX-01', 'width': 256, 'height': 64, 'pixelDensity': 2,
                   'png': 'runtime-hd/fx/fx-01.png'}
        self.hd_path = self.pack / 'manifest-tactical-runtime-hd.json'
        phases = []
        for index, name in enumerate(PHASE_ORDERS['C01-FX-01']):
            source = Image.new('RGBA', (24, 24))
            # Authored growth and displacement must survive the common transform.
            source.paste((80 + index * 20, 100, 120, 104 if index == 3 else 255),
                         (4 + index, 4, 10 + index * 3, 16))
            source.putpixel((4 + index, 3), (70, 90, 110, 51))
            path = self.pack / 'masters/fx-pilot' / f'{name}.png'
            path.parent.mkdir(parents=True, exist_ok=True)
            source.save(path)
            phases.append({'phase': name, 'source': str(path.relative_to(self.root)),
                           'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        self.entry = {'kind': 'fx-phases-v1', 'phases': phases,
                      'placement': {'scale': 1, 'origin': [4, 4], 'sourcePivot': [12, 12]}}
        self.catalog = self.root / 'catalog.json'
        self.write()

    def write(self):
        self.manifest.write_text(json.dumps({'assets': [self.asset]}))
        self.hd_path.write_text(json.dumps({'schemaVersion': 'candidate-01-runtime-hd-v2',
                                          'assetCount': 1, 'assets': [self.hd]}))
        self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': {self.asset['topicId']: self.entry}}))

    def mutate_source(self, index, change):
        entry = self.entry['phases'][index]
        path = self.root / entry['source']
        with Image.open(path) as image:
            change(image).save(path)
        entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        self.write()

    def assert_rejected(self, pattern):
        with self.assertRaisesRegex(ValueError, pattern):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())
        self.assertFalse((self.pack / 'runtime-hd').exists())

    def test_full_canvas_order_growth_fade_geometry_and_native_hashes(self):
        before = {path: path.read_bytes() for path in self.root.rglob('*') if path.is_file()}
        reports = export(self.root, self.catalog)
        self.assertEqual([r['size'] for r in reports], [[128, 32], [256, 64]])
        self.assertEqual([r['frameSize'] for r in reports], [[32, 32], [64, 64]])
        self.assertEqual([r['density'] for r in reports], [1, 2])
        self.assertEqual(reports, export(self.root, self.catalog, True))
        for path, raw in before.items():
            self.assertEqual(path.read_bytes(), raw, path)
        report = reports[0]
        self.assertEqual(report['logicalAnchor'], [16, 16])
        self.assertEqual(report['placement'], self.entry['placement'])
        self.assertEqual(report['semanticMotionReview'], 'required-separately')
        self.assertEqual(report['liveGamePlayback'], 'not-performed')
        self.assertFalse(report['independentFrameFit'])
        self.assertEqual([f['phase'] for f in report['frames']], PHASE_ORDERS['C01-FX-01'])
        with Image.open(self.root / report['output']) as sheet:
            for index, entry in enumerate(self.entry['phases']):
                with Image.open(self.root / entry['source']) as source:
                    native = sheet.crop((index * 32 + 4, 4, index * 32 + 28, 28))
                    self.assertEqual(native.tobytes(), source.tobytes())
                    self.assertEqual(report['sources'][index]['sourcePixelSha256'],
                                     hashlib.sha256(source.tobytes()).hexdigest())
                    bounds = source.getchannel('A').getbbox()
                    self.assertEqual(report['frames'][index]['alpha']['boundingBox'], [v + 4 for v in bounds])
        self.assertEqual(report['frames'][3]['alpha']['maxAlpha'], 104)
        self.assertEqual(report['sources'][3]['alpha']['maxAlpha'], 104)
        self.assertLess(reports[1]['frames'][3]['alpha']['maxAlpha'], 240)
        self.assertEqual([f['alpha']['edgeMaxAlpha'] for r in reports for f in r['frames']], [0] * 8)

    def test_reviewed_pierce_and_arrow_contracts_keep_their_own_phase_meanings(self):
        for topic, names in [('C01-FX-02', ['onset', 'sweep', 'peak', 'dissipate']),
                             ('C01-FX-03', ['approach', 'impact', 'burst', 'dissipate']),
                             ('C01-FX-04', ['contact', 'expand', 'peak', 'settle']),
                             ('C01-FX-05', ['ignite', 'rise', 'flare', 'embers']),
                             ('C01-FX-06', ['low-ring', 'rising-ring', 'sealed-ring', 'fading-fragments']),
                             ('C01-FX-07', ['airborne-burst', 'falling-pieces', 'falling-remnants', 'settled-heap']),
                             ('C01-FX-08', ['clustered-echoes', 'rising-echoes', 'dissolving-echoes', 'wisp-remnants']),
                             ('C01-FX-13', ['cracked-earth', 'ember-rise', 'flame-peak', 'ember-residue']),
                             ('C01-FX-14', ['low-fumes', 'rising-fumes', 'plume-crest', 'settling-wisps']),
                             ('C01-FX-15', ['spirit-onset', 'spirit-growth', 'leafy-spirit-peak', 'spirit-residue']),
                             ('C01-FX-16', ['short-rays', 'rising-fan', 'graylight-crest', 'residual-rays']),
                             ('C01-FX-17', ['healing-onset', 'leaf-spiral', 'healing-peak', 'orbit-dissipation']),
                             ('C01-FX-18', ['scattered-bands', 'aligning-ring', 'closed-restraints', 'settled-restraints']),
                             ('C01-FX-19', ['dense-veil', 'curling-veil', 'veil-breakup', 'leaf-wisps']),
                             ('C01-FX-20', ['bare-crescent', 'contact-spark', 'spark-spray', 'residual-sparks']),
                             ('C01-FX-21', ['debris-lift', 'rubble-scatter', 'dust-impact', 'settling-dust']),
                             ('C01-FX-22', ['loose-hardware', 'seating-impact', 'repair-swirl', 'finished-fit'])]:
            with self.subTest(topic=topic):
                asset = {**self.asset, 'topicId': topic, 'png': f'runtime/fx/fx-{topic[-2:]}.png'}
                hd = {**self.hd, 'topicId': topic, 'png': f'runtime-hd/fx/fx-{topic[-2:]}.png'}
                entry = copy.deepcopy(self.entry)
                for phase, name in zip(entry['phases'], names):
                    phase['phase'] = name
                self.manifest.write_text(json.dumps({'assets': [asset]}))
                self.hd_path.write_text(json.dumps({'schemaVersion': 'candidate-01-runtime-hd-v2',
                                                  'assetCount': 1, 'assets': [hd]}))
                self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': {topic: entry}}))
                reports = export(self.root, self.catalog)
                self.assertEqual(reports, export(self.root, self.catalog, True))
                self.assertEqual([phase['phase'] for phase in reports[0]['frames']], names)
                self.assertEqual(reports[0]['output'], str((self.pack / asset['png']).relative_to(self.root)))
                self.assertLess(reports[1]['frames'][-1]['alpha']['maxAlpha'], 240)
                entry['phases'][0]['phase'] = 'impact' if topic.endswith('02') else 'onset'
                self.catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': {topic: entry}}))
                with self.assertRaisesRegex(ValueError, 'semantic phase order'):
                    export(self.root, self.catalog)

    def use_topic(self, topic, loop):
        self.asset.update(topicId=topic, png=f'runtime/fx/fx-{topic[-2:]}.png', loop=loop)
        self.hd.update(topicId=topic, png=f'runtime-hd/fx/fx-{topic[-2:]}.png')
        for phase, name in zip(self.entry['phases'], PHASE_ORDERS[topic]):
            phase['phase'] = name
        self.write()

    def test_ambient_topics_keep_cyclic_order_and_consumer_owned_lifetime(self):
        expected = {
            'C01-FX-09': ['rainfall', 'splash-onset', 'splash-crown', 'rain-renewal'],
            'C01-FX-10': ['ripple-near', 'ripple-growing', 'ripple-wide', 'ripple-renewal'],
            'C01-FX-11': ['gust-curl-a', 'gust-curl-b', 'gust-breakup', 'gust-renewal'],
            'C01-FX-12': ['heat-curl-a', 'heat-curl-b', 'heat-split', 'heat-renewal'],
        }
        self.assertEqual(AMBIENT_PHASE_ORDERS, expected)
        for topic, order in expected.items():
            with self.subTest(topic=topic):
                self.use_topic(topic, True)
                before = {path: path.read_bytes() for path in self.root.rglob('*') if path.is_file()}
                reports = export(self.root, self.catalog)
                self.assertEqual(reports, export(self.root, self.catalog, True))
                for path, raw in before.items():
                    self.assertEqual(path.read_bytes(), raw, path)
                for report in reports:
                    self.assertEqual([p['phase'] for p in report['frames']], order)
                    self.assertIs(report['loop'], True)
                    self.assertEqual(report['fps'], 12)
                    self.assertEqual(report['blendMode'], 'normal')
                    self.assertEqual(report['logicalAnchor'], [16, 16])
                    self.assertEqual(report['placement'], self.entry['placement'])
                    self.assertEqual(report['playback'], {
                        'mode': 'continuous-loop', 'sequenceDurationMs': 1000 / 3,
                        'afterLastFrame': 'wrap-to-first-frame',
                        'wrapTransition': [order[3], order[0]], 'lifetimeOwner': 'consumer',
                    })
                    self.assertEqual(report['loopClosureReview'], 'required-separately')
                    self.assertEqual(report['semanticMotionReview'], 'required-separately')
                    self.assertEqual(report['liveGamePlayback'], 'not-performed')
                    self.assertFalse(report['independentFrameFit'])
                    self.assertEqual([p['alpha']['edgeMaxAlpha'] for p in report['frames']], [0] * 4)
                with Image.open(self.root / reports[0]['output']) as sheet:
                    for i, phase in enumerate(self.entry['phases']):
                        with Image.open(self.root / phase['source']) as source:
                            self.assertEqual(sheet.crop((i * 32 + 4, 4, i * 32 + 28, 28)).tobytes(),
                                             source.tobytes())

    def test_ambient_loop_flag_is_required_as_an_exact_boolean_before_writes(self):
        for topic in AMBIENT_PHASE_ORDERS:
            for wrong in (False, 1, 0, None, 'true'):
                with self.subTest(topic=topic, wrong=wrong):
                    self.use_topic(topic, wrong)
                    self.assert_rejected('canonical FX contract')

    def test_one_shot_topics_do_not_silently_become_loops(self):
        for topic in PHASE_ORDERS.keys() - AMBIENT_PHASE_ORDERS.keys():
            with self.subTest(topic=topic):
                self.use_topic(topic, True)
                self.assert_rejected('canonical FX contract')

    def test_loop_order_cannot_be_rotated_or_given_one_shot_labels(self):
        self.use_topic('C01-FX-09', True)
        original = copy.deepcopy(self.entry['phases'])
        for names in (PHASE_ORDERS['C01-FX-09'][1:] + PHASE_ORDERS['C01-FX-09'][:1],
                      PHASE_ORDERS['C01-FX-01']):
            self.entry['phases'] = copy.deepcopy(original)
            for phase, name in zip(self.entry['phases'], names):
                phase['phase'] = name
            self.write()
            self.assert_rejected('semantic phase order')

    def test_one_shot_report_does_not_assign_hit_lifetime_to_every_effect(self):
        for report in export(self.root, self.catalog):
            self.assertEqual(report['playback'], {
                'mode': 'once-hold', 'sequenceDurationMs': 1000 / 3,
                'afterLastFrame': 'hold-last-frame', 'wrapTransition': None,
                'lifetimeOwner': 'consumer',
            })
            self.assertEqual(report['loopClosureReview'], 'not-applicable')

    def test_hd_native_fade_retains_every_pixel_without_alpha_normalization(self):
        for index in range(4):
            self.mutate_source(index, lambda image: image.resize((64, 64), Image.Resampling.NEAREST))
        self.entry['placement'] = {'scale': .5, 'origin': [0, 0], 'sourcePivot': [32, 32]}
        self.write()
        report = export(self.root, self.catalog)[1]
        with Image.open(self.root / report['output']) as sheet:
            for index, entry in enumerate(self.entry['phases']):
                with Image.open(self.root / entry['source']) as source:
                    self.assertEqual(sheet.crop((index * 64, 0, (index + 1) * 64, 64)).tobytes(), source.tobytes())
        self.assertEqual(report['frames'][3]['alpha']['maxAlpha'], 104)

    def test_unit_and_prop_alpha_gate_still_rejects_translucent_fade(self):
        with Image.open(self.root / self.entry['phases'][3]['source']) as source:
            self.assertEqual(validate_fx_alpha(source, 'fx')['maxAlpha'], 104)
            with self.assertRaisesRegex(ValueError, 'near-opaque'):
                validate_alpha(source, 'unit or prop')

    def test_arbitrary_phase_names_wrong_order_and_duplicate_labels_rejected(self):
        original = copy.deepcopy(self.entry)
        for order in (['a', 'b', 'c', 'd'], ['sweep', 'onset', 'peak', 'dissipate'],
                      ['onset', 'sweep', 'peak', 'peak']):
            with self.subTest(order=order):
                self.entry = copy.deepcopy(original)
                for entry, name in zip(self.entry['phases'], order):
                    entry['phase'] = name
                self.write()
                self.assert_rejected('semantic phase order')

    def test_duplicate_pixels_rejected_even_when_encoded_files_differ(self):
        first, last = self.entry['phases'][0], self.entry['phases'][3]
        with Image.open(self.root / first['source']) as source:
            source.save(self.root / last['source'], compress_level=0)
        last['sha256'] = hashlib.sha256((self.root / last['source']).read_bytes()).hexdigest()
        self.assertNotEqual(first['sha256'], last['sha256'])
        self.write()
        self.assert_rejected('duplicate decoded')

    def test_distinct_source_hashes_cannot_hide_duplicate_output_phases(self):
        first, last = self.entry['phases'][0], self.entry['phases'][3]
        with Image.open(self.root / first['source']) as source:
            source.putpixel((0, 0), (1, 2, 3, 0))
            source.save(self.root / last['source'])
        last['sha256'] = hashlib.sha256((self.root / last['source']).read_bytes()).hexdigest()
        self.entry['placement'] = {'scale': .5, 'origin': [10, 10], 'sourcePivot': [12, 12]}
        self.write()
        self.assert_rejected('duplicate rendered')

    def test_canvas_mismatch_rejected(self):
        self.mutate_source(3, lambda image: image.resize((25, 24)))
        self.assert_rejected('canvas dimensions')

    def test_shared_transform_clipping_aspect_rounding_and_invalid_pivot_rejected(self):
        for placement, error in [
            ({'scale': 1, 'origin': [10, 4], 'sourcePivot': [6, 12]}, 'clip full authored'),
            ({'scale': .49, 'origin': [4, 4], 'sourcePivot': [12, 12]}, 'integer canvas'),
            ({'scale': 1, 'origin': [4, 4], 'sourcePivot': [12, 16]}, 'sourcePivot'),
            ({'scale': 1, 'origin': [4, 4], 'sourcePivot': [-1, 12]}, 'sourcePivot'),
            ({'scale': 0, 'origin': [4, 4], 'sourcePivot': [12, 12]}, 'finite positive'),
            ({'scale': True, 'origin': [4, 4], 'sourcePivot': [12, 12]}, 'finite positive'),
            ({'scale': float('nan'), 'origin': [4, 4], 'sourcePivot': [12, 12]}, 'finite positive'),
            ({'scale': 1, 'origin': [4.5, 4], 'sourcePivot': [11.5, 12]}, 'integer origin'),
        ]:
            with self.subTest(placement=placement):
                self.entry['placement'] = placement
                self.write()
                self.assert_rejected(error)

    def test_no_crop_fit_or_individual_phase_transform_fields(self):
        original = copy.deepcopy(self.entry)
        for key, value in [('crop', [1, 1, 23, 23]), ('fit', 'alpha-bbox'), ('registrationOffset', [1, 0])]:
            for destination in ('placement', 'phase'):
                with self.subTest(key=key, destination=destination):
                    self.entry = copy.deepcopy(original)
                    target = self.entry['placement'] if destination == 'placement' else self.entry['phases'][1]
                    target[key] = value
                    self.write()
                    self.assert_rejected('no crop|semantic phase order')

    def test_source_alpha_empty_opaque_and_edge_residue_rejected(self):
        for alpha in (0, 255):
            image = Image.new('RGBA', (24, 24), (80, 90, 100, alpha))
            with self.assertRaisesRegex(ValueError, 'transparent exterior and nonempty'):
                validate_fx_alpha(image, 'invalid')
        for alpha in (1, 104, 255):
            with self.subTest(alpha=alpha):
                image = Image.new('RGBA', (24, 24))
                image.putpixel((0, 5), (80, 90, 100, alpha))
                with self.assertRaisesRegex(ValueError, 'outer edge'):
                    validate_fx_alpha(image, 'edge')

    def test_source_edge_rejected_before_writes(self):
        def touch_edge(image):
            image.putpixel((0, 10), (80, 90, 100, 2))
            return image
        self.mutate_source(3, touch_edge)
        self.assert_rejected('outer edge')

    def test_source_alpha_one_residue_is_preserved_but_output_margin_stays_zero(self):
        def residue(image):
            image.putpixel((0, 10), (80, 90, 100, 1))
            return image
        self.mutate_source(3, residue)
        path = self.root / self.entry['phases'][3]['source']
        original = path.read_bytes()
        report = export(self.root, self.catalog)
        self.assertEqual(path.read_bytes(), original)
        self.assertEqual(report[0]['sources'][3]['alpha']['edgeMaxAlpha'], 1)
        self.assertEqual(report[0]['sources'][3]['alpha']['edgeNonzeroPixels'], 1)
        for output in report:
            self.assertEqual(output['frames'][3]['alpha']['edgeMaxAlpha'], 0)
            self.assertEqual(output['frames'][3]['alpha']['allowedEdgeMaxAlpha'], 0)
        with Image.open(self.root / report[0]['output']) as sheet:
            self.assertEqual(sheet.getpixel((3 * 32 + 4, 10 + 4)), (80, 90, 100, 1))
        self.assertEqual(report, export(self.root, self.catalog, True))

    def test_each_output_cell_edge_gated_even_with_clean_source_edges(self):
        # Downsampling can bring content onto the output perimeter: no silent trim.
        for index in range(4):
            def close_to_edge(image):
                canvas = Image.new('RGBA', (128, 128))
                canvas.paste(image, (1, 50))
                canvas.putpixel((1, 64), (70, 90, 110, 255))
                return canvas
            self.mutate_source(index, close_to_edge)
        self.entry['placement'] = {'scale': .25, 'origin': [0, 0], 'sourcePivot': [64, 64]}
        self.write()
        self.assert_rejected('outer edge')

    def test_hash_mismatch_and_non_png_rgba_rejected(self):
        path = self.root / self.entry['phases'][3]['source']
        path.write_bytes(path.read_bytes() + b'changed')
        self.assert_rejected('SHA-256 mismatch')
        self.mutate_source(3, lambda image: image.convert('RGB'))
        self.assert_rejected('PNG RGBA')

    def test_source_ownership_escape_and_symlink_rejected(self):
        original = copy.deepcopy(self.entry['phases'][0])
        outside = self.root / 'outside.png'
        outside.write_bytes((self.root / original['source']).read_bytes())
        alias = self.pack / 'masters/fx-pilot/alias.png'
        alias.symlink_to(outside)
        for path in ('../outside.png', str(outside), 'outside.png', str(alias.relative_to(self.root))):
            with self.subTest(path=path):
                self.entry['phases'][0] = {**original, 'source': path}
                self.write()
                self.assert_rejected('relative path|escapes|pack masters')

    def test_manifest_geometry_timing_blend_and_anchor_rejected(self):
        original = copy.deepcopy(self.asset)
        for key, value in [('width', 256), ('frameHeight', 64), ('frames', 1), ('fps', 24),
                           ('loop', True), ('blendMode', 'screen'), ('anchor', [16, 24]), ('category', 'combat-unit')]:
            with self.subTest(key=key):
                self.asset = {**original, key: value}
                self.write()
                self.assert_rejected('canonical FX contract')

    def test_hd_required_and_wrong_density_or_geometry_rejected(self):
        original = copy.deepcopy(self.hd)
        for delta in ({'pixelDensity': 1}, {'pixelDensity': 3}, {'pixelDensity': True}, {'width': 255}, {'height': 32}):
            with self.subTest(delta=delta):
                self.hd = {**original, **delta}
                self.write()
                self.assert_rejected('HD contract')
        self.hd_path.unlink()
        self.assert_rejected('HD contract')

    def test_wrong_export_paths_rejected_before_either_density_written(self):
        for path in ('runtime-hd/fx/other.png', '../outside.png', 'runtime-hd/fx/../fx/fx-01.png',
                     'masters/fx-pilot/onset.png', 'runtime/fx/fx-01.png'):
            with self.subTest(path=path):
                self.hd['png'] = path
                self.write()
                self.assert_rejected('export path|escapes')

    def test_export_symlink_or_hardlink_cannot_overwrite_source(self):
        source = self.root / self.entry['phases'][0]['source']
        before = source.read_bytes()
        target = self.pack / self.hd['png']
        target.parent.mkdir(parents=True)
        for link in ('symlink', 'hardlink'):
            if link == 'symlink':
                target.symlink_to(source)
            else:
                target.hardlink_to(source)
            with self.assertRaisesRegex(ValueError, 'export path'):
                export(self.root, self.catalog)
            self.assertFalse((self.pack / 'runtime').exists())
            self.assertEqual(source.read_bytes(), before)
            target.unlink()

    def test_bad_hd_destination_ancestor_rejected_before_base_write(self):
        (self.pack / 'runtime-hd').write_bytes(b'not a directory')
        with self.assertRaisesRegex(ValueError, 'export path'):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())
        self.assertEqual((self.pack / 'runtime-hd').read_bytes(), b'not a directory')

    def test_symlinked_masters_cannot_make_export_overwrite_a_registered_source(self):
        (self.pack / 'masters').rename(self.pack / 'runtime')
        (self.pack / 'runtime/fx-pilot').rename(self.pack / 'runtime/fx')
        (self.pack / 'runtime/fx/onset.png').rename(self.pack / 'runtime/fx/fx-01.png')
        (self.pack / 'masters').symlink_to(self.pack / 'runtime', target_is_directory=True)
        for index, entry in enumerate(self.entry['phases']):
            name = 'fx-01' if index == 0 else entry['phase']
            entry['source'] = str((self.pack / f'masters/fx/{name}.png').relative_to(self.root))
        self.write()
        before = {path: path.read_bytes() for path in (self.pack / 'runtime').rglob('*.png')}
        with self.assertRaisesRegex(ValueError, 'canonical owned directory'):
            export(self.root, self.catalog)
        self.assertEqual(before, {path: path.read_bytes() for path in (self.pack / 'runtime').rglob('*.png')})
        self.assertFalse((self.pack / 'runtime-hd').exists())

    def test_special_output_and_source_files_rejected_without_blocking_or_writes(self):
        target = self.pack / self.hd['png']
        target.parent.mkdir(parents=True)
        os.mkfifo(target)
        with self.assertRaisesRegex(ValueError, 'export path'):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())
        target.unlink()
        source = self.root / self.entry['phases'][0]['source']
        source.unlink()
        os.mkfifo(source)
        with self.assertRaisesRegex(ValueError, 'regular file'):
            export(self.root, self.catalog)
        self.assertFalse((self.pack / 'runtime').exists())

    def test_strict_schema_duplicate_keys_and_later_bad_topic_prevent_writes(self):
        valid = json.loads(self.catalog.read_text())
        cases = [([], 'schemaVersion'), ({**valid, 'extra': 1}, 'schemaVersion'),
                 ({**valid, 'schemaVersion': True}, 'schemaVersion'),
                 ({**valid, 'assets': {**valid['assets'], 'C01-FX-02': self.entry}}, 'phase contract')]
        for data, error in cases:
            with self.subTest(data=data):
                self.catalog.write_text(json.dumps(data))
                self.assert_rejected(error)
        self.catalog.write_text('{"schemaVersion":1,"assets":{},"assets":{}}')
        self.assert_rejected('duplicate JSON key')

    def test_check_detects_stale_hd_without_rewriting_anything(self):
        reports = export(self.root, self.catalog)
        Image.new('RGBA', (256, 64)).save(self.root / reports[1]['output'])
        before = {path: path.read_bytes() for path in self.root.rglob('*') if path.is_file()}
        with self.assertRaisesRegex(ValueError, 'stale FX export'):
            export(self.root, self.catalog, True)
        self.assertEqual(before, {path: path.read_bytes() for path in self.root.rglob('*') if path.is_file()})

    def test_empty_catalog_makes_no_changes(self):
        self.catalog.write_text('{"schemaVersion":1,"assets":{}}')
        self.assertEqual(export(self.root, self.catalog), [])
        self.assertFalse((self.pack / 'runtime').exists())


if __name__ == '__main__':
    unittest.main()
