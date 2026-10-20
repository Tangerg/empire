import json
import tempfile
import unittest
from pathlib import Path
from update_approved_ledger import build, DOC


class ApprovedLedgerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / DOC / 'approved-v5').mkdir(parents=True)
        self.manifest = 'pack/manifest.json'
        (self.root / 'pack').mkdir()
        self.topic = {'topicId': 'C01-UNIT-MAGE', 'category': 'combat-unit', 'png': 'runtime/mage.png', 'qualityTier': 'legacy'}
        self.write('pack/manifest.json', {'assets': [self.topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**self.topic, 'manifest': self.manifest, 'campaign': 'story-candidate-01', 'label': 'Mage'}])
        self.write(DOC / 'environment-atlas-manifest.json', [{'id': 'forest', 'componentCount': 16}])
        self.progress = {'topicStages': {}, 'formalRuntimeBoundTopics': 0}
        self.write(DOC / 'approved-v5/progress.json', self.progress)

    def write(self, relative, value):
        (self.root / relative).write_text(json.dumps(value))

    def test_raw_does_not_count_as_formal_or_full_animation(self):
        raw = self.root / 'pack/masters/approved-v5/animation/mage/idle'
        raw.mkdir(parents=True)
        (raw / 'master.png').write_bytes(b'preserved source placeholder')
        result = build(self.root)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(result['summary']['formalBound'], 0)
        self.assertEqual(result['canonicalTopics'][0]['export'], 'pending')

    def test_old_quality_and_environment_outputs_are_not_canonical_completions(self):
        self.progress['environmentCells'] = {'forest': {'formalRuntimeBoundCells': ['oak']}}
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 0)
        self.assertEqual(result['summary']['registeredEnvironmentCells'], 1)
        self.assertEqual(result['summary']['environmentComponentScope'], 16)

    def test_formal_stage_requires_matching_manifest(self):
        self.progress['topicStages'] = {'C01-UNIT-MAGE': {'raw': 'preserved'}}
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        with self.assertRaisesRegex(ValueError, 'mismatch'):
            build(self.root)

    def test_one_animation_counts_one_topic(self):
        for pose in ['idle', 'walk-a', 'attack', 'walk-b']:
            raw = self.root / 'pack/masters/approved-v5/animation/mage' / pose
            raw.mkdir(parents=True)
            (raw / 'master.png').write_bytes(b'preserved source placeholder')
        self.topic['qualityTier'] = 'approved-chunky-v5-export-verified'
        self.write('pack/manifest.json', {'assets': [self.topic]})
        self.progress.update(topicStages={'C01-UNIT-MAGE': {'raw': '4 frames'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 1)
        self.assertEqual(result['summary']['liveGameVerified'], 0)

    def test_formal_binding_without_native_source_is_rejected(self):
        self.topic['qualityTier'] = 'approved-chunky-v5-export-verified'
        self.write('pack/manifest.json', {'assets': [self.topic]})
        self.progress.update(topicStages={'C01-UNIT-MAGE': {'raw': 'claimed'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
            build(self.root)

    def test_canonical_terrain_counts_source_once_not_mask_variants(self):
        topic = {**self.topic, 'topicId': 'C01-TERRAIN-BORDER-2', 'category': 'terrain'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-01', 'label': 'Rain mud'}])
        raw = self.root / 'pack/masters/approved-v5/canonical-terrain/border-2'
        raw.mkdir(parents=True)
        (raw / 'master.png').write_bytes(b'preserved source placeholder')
        result = build(self.root)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(result['summary']['formalBound'], 0)

    def test_scifi_terrain_and_chinese_props_require_their_selected_source_directory(self):
        cases = [
            ('c02-terrain-hesha-glass-sea', 'terrain', 'story-candidate-02',
             'canonical-terrain/hesha-glass-sea'),
            ('c03-battle-prop-0fb580c9', 'battle-prop', 'story-candidate-03',
             'props/c03-battle-prop-0fb580c9'),
        ]
        for identity, category, campaign, directory in cases:
            with self.subTest(topic=identity):
                topic = {**self.topic, 'topicId': identity, 'category': category,
                         'qualityTier': 'approved-chunky-v5-export-verified'}
                self.write('pack/manifest.json', {'assets': [topic]})
                self.write(DOC / 'canonical-topic-manifest.json', [{**topic,
                           'manifest': self.manifest, 'campaign': campaign, 'label': identity}])
                self.progress.update(topicStages={identity: {'raw': 'reviewed'}},
                                     formalRuntimeBoundTopics=1)
                self.write(DOC / 'approved-v5/progress.json', self.progress)
                unrelated = self.root / 'pack/masters/approved-v5/unrelated' / identity
                unrelated.mkdir(parents=True)
                (unrelated / 'master.png').write_bytes(b'wrong directory cannot substitute')
                with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
                    build(self.root)
                raw = self.root / 'pack/masters/approved-v5' / directory
                raw.mkdir(parents=True)
                (raw / 'master.png').write_bytes(b'independent native source')
                (raw / 'raw.png').write_bytes(b'duplicate does not add a topic')
                result = build(self.root)
                self.assertEqual(result['summary']['formalBound'], 1)
                self.assertEqual(result['summary']['rawPreservedTopics'], 1)
                self.assertEqual(result['summary']['liveGameVerified'], 0)
                self.assertEqual(result['canonicalTopics'][0]['selectedRawMasters'],
                                 [str((raw / 'master.png').relative_to(self.root))])

    def test_versioned_static_selection_uses_catalog_owner_not_default_master(self):
        identity = 'c03-battle-prop-27872fa1'
        topic = {**self.topic, 'topicId': identity, 'category': 'battle-prop',
                 'qualityTier': 'approved-chunky-v5-export-verified'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic,
                   'manifest': self.manifest, 'campaign': 'story-candidate-03', 'label': identity}])
        self.progress.update(topicStages={identity: {'raw': 'versioned source selected'}},
                             formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        directory = Path('pack/masters/approved-v5/props') / identity
        (self.root / directory).mkdir(parents=True)
        (self.root / directory / 'master.png').write_bytes(b'preserved original, not selected')
        version = directory / 'candidates/matte-v2/master.png'
        (self.root / version).parent.mkdir(parents=True)
        (self.root / version).write_bytes(b'new selected native source')
        (self.root / 'tools/art').mkdir(parents=True)
        self.write('tools/art/source-overrides.json', {'assets': {identity: {'source': str(version)}}})
        result = build(self.root)
        self.assertEqual(result['canonicalTopics'][0]['selectedRawMasters'], [str(version)])
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        (self.root / version).unlink()
        with self.assertRaisesRegex(ValueError, 'selected approved static source is missing'):
            build(self.root)

    def test_skill_master_uses_its_native_skill_directory(self):
        topic = {**self.topic, 'topicId': 'C01-SKILL-01', 'category': 'skill'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-01', 'label': 'Occupy'}])
        raw = self.root / 'pack/masters/approved-v5/skill/skill-01'
        raw.mkdir(parents=True)
        (raw / 'master.png').write_bytes(b'preserved source placeholder')
        result = build(self.root)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(result['summary']['formalBound'], 0)

    def test_scifi_hud_master_uses_its_full_identity_directory(self):
        topic = {**self.topic, 'topicId': 'c02-hud-ally', 'category': 'hud'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-02', 'label': 'Ally'}])
        raw = self.root / 'pack/masters/approved-v5/hud/c02-hud-ally'
        raw.mkdir(parents=True)
        (raw / 'master.png').write_bytes(b'preserved source placeholder')
        result = build(self.root)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(result['summary']['formalBound'], 0)

    def test_scifi_skill_requires_own_full_identity_source(self):
        topic = {**self.topic, 'topicId': 'c02-skill-assault', 'category': 'skill', 'qualityTier': 'approved-chunky-v5-export-verified'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest, 'campaign': 'story-candidate-02', 'label': 'Assault'}])
        self.progress.update(topicStages={topic['topicId']: {'raw': 'reviewed'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
            build(self.root)
        raw = self.root / 'pack/masters/approved-v5/skills/c02-skill-assault'
        raw.mkdir(parents=True)
        (raw / 'master.png').write_bytes(b'preserved independent source')
        (raw / 'raw.png').write_bytes(b'duplicate cannot count twice')
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 1)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(len(result['canonicalTopics'][0]['selectedRawMasters']), 1)

    def test_scifi_status_requires_own_full_identity_source(self):
        topic = {**self.topic, 'topicId': 'c02-status-water', 'category': 'status', 'qualityTier': 'approved-chunky-v5-export-verified'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest, 'campaign': 'story-candidate-02', 'label': 'Water'}])
        self.progress.update(topicStages={topic['topicId']: {'raw': 'reviewed'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
            build(self.root)
        raw = self.root / 'pack/masters/approved-v5/status/c02-status-water'
        raw.mkdir(parents=True)
        (raw / 'master.png').write_bytes(b'preserved independent source')
        (raw / 'raw.png').write_bytes(b'duplicate cannot count twice')
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 1)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(len(result['canonicalTopics'][0]['selectedRawMasters']), 1)

    def test_full_scifi_topic_identity_locates_raw_without_counting_binding(self):
        topic = {**self.topic, 'topicId': 'c02-battle-prop-cargo-crates', 'category': 'battle-prop'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-02', 'label': 'Cargo crates'}])
        raw = self.root / 'pack/masters/approved-v5/props/c02-battle-prop-cargo-crates'
        raw.mkdir(parents=True)
        (raw / 'master.png').write_bytes(b'preserved source placeholder')
        result = build(self.root)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(result['summary']['formalBound'], 0)
        self.assertEqual(result['summary']['liveGameVerified'], 0)

    def test_character_card_counts_one_topic_only_with_independent_native_character(self):
        topic = {**self.topic, 'topicId': 'C01-CHAR-LEIA-01', 'category': 'narrative-static', 'kind': 'character-card', 'qualityTier': 'approved-chunky-v5-export-verified'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest, 'campaign': 'story-candidate-01', 'label': 'Laiya'}])
        self.progress.update(topicStages={topic['topicId']: {'raw': 'independent transparent character and separate UI background'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        background = self.root / 'pack/masters/approved-v5/narrative/card-background'
        background.mkdir(parents=True)
        (background / 'master.png').write_bytes(b'background is not a character')
        with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
            build(self.root)
        character = self.root / 'pack/masters/approved-v5/narrative/char-leia-01'
        character.mkdir(parents=True)
        (character / 'master.png').write_bytes(b'independent character placeholder')
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 1)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(len(result['canonicalTopics'][0]['selectedRawMasters']), 1)
        self.assertNotIn('card-background', result['canonicalTopics'][0]['selectedRawMasters'][0])

    def test_status_and_hud_sources_use_native_category_without_inflating_stage(self):
        for category in ['status', 'hud']:
            with self.subTest(category=category):
                topic = {**self.topic, 'topicId': f'C01-{category.upper()}-01', 'category': category}
                self.write('pack/manifest.json', {'assets': [topic]})
                self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest, 'campaign': 'story-candidate-01', 'label': category}])
                raw = self.root / f'pack/masters/approved-v5/{category}/{category}-01'
                raw.mkdir(parents=True)
                (raw / 'master.png').write_bytes(b'preserved source placeholder')
                result = build(self.root)
                self.assertEqual(result['summary']['rawPreservedTopics'], 1)
                self.assertEqual(result['summary']['formalBound'], 0)
                self.assertEqual(result['summary']['liveGameVerified'], 0)

    def test_effect_phases_count_one_topic_after_formal_binding_only(self):
        topic = {**self.topic, 'topicId': 'C01-FX-01', 'category': 'fx'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-01', 'label': 'Slash'}])
        for phase in ['onset', 'sweep', 'peak', 'dissipate']:
            raw = self.root / 'pack/masters/approved-v5/fx/fx-01' / phase / 'master.png'
            raw.parent.mkdir(parents=True)
            raw.write_bytes(b'immutable phase placeholder')
        result = build(self.root)
        self.assertEqual(result['summary']['rawPreservedTopics'], 1)
        self.assertEqual(result['summary']['formalBound'], 0)
        self.assertEqual(len(result['canonicalTopics'][0]['selectedRawMasters']), 4)
        topic['qualityTier'] = 'approved-chunky-v5-export-verified'
        self.write('pack/manifest.json', {'assets': [topic]})
        self.progress.update(topicStages={topic['topicId']: {'raw': 'four phases'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        result = build(self.root)
        self.assertEqual(result['summary']['boundByCategory'], {'fx': 1})
        self.assertEqual(result['summary']['liveGameVerified'], 0)

    def test_faction_kit_counts_one_native_group_not_its_components(self):
        topic = {**self.topic, 'topicId': 'C01-FACTION-LORNE', 'category': 'faction-kit'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-01', 'label': 'Lorne kit'}])
        raw = self.root / 'pack/masters/approved-v5/faction-kit/faction-lorne/master.png'
        raw.parent.mkdir(parents=True)
        raw.write_bytes(b'one immutable shield-cloth-sword group')
        self.assertEqual(build(self.root)['summary']['formalBound'], 0)
        topic['qualityTier'] = 'approved-chunky-v5-export-verified'
        self.write('pack/manifest.json', {'assets': [topic]})
        self.progress.update(topicStages={topic['topicId']: {'raw': 'one group'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        result = build(self.root)
        self.assertEqual(result['summary']['boundByCategory'], {'faction-kit': 1})
        self.assertEqual(result['summary']['liveGameVerified'], 0)
        raw.unlink()
        with self.assertRaisesRegex(ValueError, 'lacks a discoverable'):
            build(self.root)

    def test_scene_master_is_discovered_only_for_canonical_scene_kind(self):
        for topic_id in ['C01-CH01-S05', 'C01-CH01-S01', 'C01-ARCH-THREE-BRIDGES', 'C01-ARCH-REDSTONE', 'C01-CH03-S11', 'C01-CH01-S00']:
            with self.subTest(topic=topic_id):
                topic = {**self.topic, 'topicId': topic_id, 'category': 'narrative-static', 'kind': 'scene'}
                self.write('pack/manifest.json', {'assets': [topic]})
                self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                           'campaign': 'story-candidate-01', 'label': 'Scene'}])
                raw = self.root / 'pack/masters/approved-v5/narrative' / topic_id.removeprefix('C01-').lower() / 'master.png'
                raw.parent.mkdir(parents=True)
                raw.write_bytes(b'complete immutable opaque scene placeholder')
                result = build(self.root)
                self.assertEqual(result['summary']['rawPreservedTopics'], 1)
                self.assertEqual(result['summary']['formalBound'], 0)
                self.assertEqual(result['canonicalTopics'][0]['selectedRawMasters'], [str(raw.relative_to(self.root))])
        topic = {**topic, 'qualityTier': 'approved-chunky-v5-export-verified'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.progress.update(topicStages={topic_id: {'raw': 'complete opaque scene', 'export': 'explicit framing reviewed'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 1)
        self.assertEqual(result['summary']['liveGameVerified'], 0)
        raw.unlink()
        with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
            build(self.root)

        topic = {**topic, 'topicId': 'C01-CH01-S00', 'kind': 'key-prop'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-01', 'label': 'Deliberately reverted scene kind must not discover scene master'}])
        raw = self.root / 'pack/masters/approved-v5/narrative/ch01-s00/master.png'
        raw.parent.mkdir(parents=True, exist_ok=True)
        raw.write_bytes(b'scene-like pixels do not change the canonical kind')
        self.progress.update(topicStages={topic['topicId']: {'raw': 'claimed'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
            build(self.root)

    def test_opening_scene_kind_correction_alone_adds_no_completion(self):
        topic = {**self.topic, 'topicId': 'C01-CH01-S00', 'category': 'narrative-static', 'kind': 'scene'}
        self.write('pack/manifest.json', {'assets': [topic]})
        self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                   'campaign': 'story-candidate-01', 'label': 'Roderick tying the wristguard'}])
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 0)
        self.assertEqual(result['summary']['rawPreservedTopics'], 0)
        self.assertEqual(result['summary']['liveGameVerified'], 0)
        row = result['canonicalTopics'][0]
        self.assertEqual(row['selectedRawMasters'], [])
        for stage in ('raw', 'export', 'formalBinding', 'liveGame'):
            self.assertEqual(row[stage], 'pending')

    def test_reviewed_narrative_props_discover_own_native_master_without_counting_consumers(self):
        for suffix in ['LEFT-GLOVE', 'EXILE-HARDTACK', 'IVRA-BROKEN-BRIDLE', 'CROWN-FRAGMENT']:
            with self.subTest(prop=suffix):
                topic = {**self.topic, 'topicId': f'C01-PROP-{suffix}',
                         'category': 'narrative-static', 'kind': 'key-prop'}
                self.write('pack/manifest.json', {'assets': [topic]})
                self.write(DOC / 'canonical-topic-manifest.json', [{**topic, 'manifest': self.manifest,
                    'campaign': 'story-candidate-01', 'label': suffix}])
                self.progress.update(topicStages={}, formalRuntimeBoundTopics=0)
                self.write(DOC / 'approved-v5/progress.json', self.progress)
                raw = self.root / f'pack/masters/approved-v5/narrative-props/prop-{suffix.lower()}/master.png'
                raw.parent.mkdir(parents=True)
                raw.write_bytes(b'independent immutable transparent prop placeholder')
                result = build(self.root)
                self.assertEqual(result['summary']['rawPreservedTopics'], 1)
                self.assertEqual(result['summary']['formalBound'], 0)
                self.assertEqual(result['canonicalTopics'][0]['selectedRawMasters'], [str(raw.relative_to(self.root))])
                topic['qualityTier'] = 'approved-chunky-v5-export-verified'
                self.write('pack/manifest.json', {'assets': [topic]})
                self.progress.update(topicStages={topic['topicId']: {'raw': 'independent transparent prop',
                    'export': 'canonical128 reviewed; catalog only'}}, formalRuntimeBoundTopics=1)
                self.write(DOC / 'approved-v5/progress.json', self.progress)
                result = build(self.root)
                self.assertEqual(result['summary']['boundByCategory'], {'narrative-static': 1})
                self.assertEqual(result['summary']['liveGameVerified'], 0)
                raw.unlink()
                with self.assertRaisesRegex(ValueError, 'lacks a discoverable approved native master'):
                    build(self.root)
