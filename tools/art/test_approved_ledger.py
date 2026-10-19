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
        self.topic['qualityTier'] = 'approved-chunky-v5-export-verified'
        self.write('pack/manifest.json', {'assets': [self.topic]})
        self.progress.update(topicStages={'C01-UNIT-MAGE': {'raw': '4 frames'}}, formalRuntimeBoundTopics=1)
        self.write(DOC / 'approved-v5/progress.json', self.progress)
        result = build(self.root)
        self.assertEqual(result['summary']['formalBound'], 1)
        self.assertEqual(result['summary']['liveGameVerified'], 0)
