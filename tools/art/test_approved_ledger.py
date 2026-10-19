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
