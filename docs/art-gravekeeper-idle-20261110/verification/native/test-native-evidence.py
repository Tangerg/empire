#!/usr/bin/env python3
"""Isolated in-memory controls. Synthetic fixtures are never native evidence."""
import copy
import importlib.util
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('native_evidence', HERE / 'verify-native-evidence.py')
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


def actor(art, frame, mirrored):
    scale = .0792 if art == v.IDLE else .0921053
    return {'id': 4, 'art': art, 'frame': frame, 'mirrored': mirrored,
            'scale': [-scale if mirrored else scale, scale], 'position': [500, 300], 'supportPlane': [500, 300],
            'offset': (v.IDLE_OFFSETS if art == v.IDLE else v.CAST_OFFSETS)[frame]}


def idle(mirrored=False):
    return [{'captureMode': 'idle', 'captureStride': 3, 'observedFrame': (i+1)*3,
             'processFrame': 1000+i*3, 'elapsedMs': i*100, 'presenting': False,
             'actors': [actor(v.IDLE, v.frame_at((875+i*50)%2400, v.IDLE_ENDS), mirrored)]}
            for i in range(103)]


def recovery():
    result = []
    for i in range(48):
        elapsed = (i+1)*1000/60
        art, frame = (v.CAST, v.frame_at(elapsed, v.CAST_ENDS)) if elapsed < 600-1e-7 else (v.IDLE, 0)
        result.append({'captureMode': 'motion', 'captureStride': 1, 'observedFrame': i+1,
                       'processFrame': 2000+i, 'elapsedMs': i*30, 'presenting': elapsed < 800-1e-7,
                       'actors': [actor(art, frame, True)]})
    return result


def save():
    return {'campaign': {'savedAt': 'old-campaign', 'state': {'sentinel': True}},
            'battle': {'savedAt': 'old-battle', 'state': {'units': [
                {'id': 4, 'type': 'c01.gravekeeper', 'facing': 'east', 'hp': 4},
                {'id': 3, 'type': 'c01.archer', 'facing': 'west', 'hp': 39}]}}, 'events': []}


def fresh(document):
    result = copy.deepcopy(document)
    result['campaign']['savedAt'] = 'new-campaign'
    result['battle']['savedAt'] = 'new-battle'
    return result


class NativeEvidenceControls(unittest.TestCase):
    def test_positive_both_idle_directions(self):
        for mirrored, kind in [(False, 'idle-right'), (True, 'idle-left')]:
            self.assertEqual(v.verify_trace(kind, idle(mirrored))['simulationSecondsFirstToLast'], 5.1)

    def test_positive_recovery_with_actual_count(self):
        result = v.verify_trace('recovery-left', recovery())
        self.assertEqual(result['observations'], 48)
        self.assertEqual(result['recoveryObservation'], 35)

    def test_duplicate_process_frame_rejected(self):
        traces = idle(); traces[42]['processFrame'] = traces[41]['processFrame']
        with self.assertRaises(ValueError): v.verify_trace('idle-right', traces)

    def test_wrong_observed_stride_rejected(self):
        traces = idle(); traces[20]['observedFrame'] += 1
        with self.assertRaises(ValueError): v.verify_trace('idle-right', traces)

    def test_short_capture_rejected(self):
        with self.assertRaises(ValueError): v.verify_trace('idle-right', idle()[:-1])

    def test_shifted_pose_rejected(self):
        traces = idle(); traces[20]['actors'][0] = actor(v.IDLE, 1, False)
        with self.assertRaises(ValueError): v.verify_trace('idle-right', traces)

    def test_wrong_direction_rejected(self):
        with self.assertRaises(ValueError): v.verify_trace('idle-left', idle())

    def test_scale_change_rejected(self):
        traces = idle(); traces[3]['actors'][0]['scale'] = [.08, .08]
        with self.assertRaises(ValueError): v.verify_trace('idle-right', traces)

    def test_contact_jump_rejected(self):
        traces = idle(); traces[3]['actors'][0]['position'] = [501, 300]
        with self.assertRaises(ValueError): v.verify_trace('idle-right', traces)

    def test_registration_change_rejected(self):
        traces = idle(); traces[3]['actors'][0]['offset'] = [-230, -482]
        with self.assertRaises(ValueError): v.verify_trace('idle-right', traces)

    def test_idle_presentation_rejected(self):
        traces = idle(); traces[3]['presenting'] = True
        with self.assertRaises(ValueError): v.verify_trace('idle-right', traces)

    def test_early_recovery_rejected(self):
        traces = recovery(); traces[34]['actors'][0] = actor(v.IDLE, 0, True)
        with self.assertRaises(ValueError): v.verify_trace('recovery-left', traces)

    def test_internal_boundary_roundoff_accepted(self):
        traces = recovery(); traces[29]['actors'][0] = actor(v.CAST, 4, True)
        v.verify_trace('recovery-left', traces)

    def test_internal_boundary_whole_frame_shift_rejected(self):
        traces = recovery(); traces[30]['actors'][0] = actor(v.CAST, 4, True)
        with self.assertRaises(ValueError): v.verify_trace('recovery-left', traces)

    def test_late_source_endpoint_rejected(self):
        traces = recovery(); traces[35]['actors'][0] = actor(v.CAST, 5, True)
        with self.assertRaises(ValueError): v.verify_trace('recovery-left', traces)

    def test_early_receipt_completion_rejected(self):
        traces = recovery()[:-1]; traces[-1]['presenting'] = False
        with self.assertRaises(ValueError): v.verify_trace('recovery-left', traces)

    def test_missing_recovery_endpoint_rejected(self):
        with self.assertRaises(ValueError): v.verify_trace('recovery-left', recovery()[:-1])

    def test_rgb_mutation_rejected(self):
        pixels = b'isolated synthetic RGB fixture'
        v.verify_rgb(pixels, v.sha(pixels))
        with self.assertRaises(ValueError): v.verify_rgb(pixels + b'x', v.sha(pixels))

    def test_source_and_hash_mismatch_rejected(self):
        v.verify_identity([v.SOURCE, v.GODOT_TREE, v.TOOLS_TREE], v.ASSEMBLIES)
        with self.assertRaises(ValueError): v.verify_identity(['wrong', v.GODOT_TREE, v.TOOLS_TREE], v.ASSEMBLIES)
        with self.assertRaises(ValueError): v.verify_identity([v.SOURCE, v.GODOT_TREE, v.TOOLS_TREE], {})

    def test_timestamp_only_save_accepted(self):
        original = save()
        self.assertTrue(v.verify_saves('idle-right', original, fresh(original), original, original)['freshOfficialSaveTimestamps'])

    def test_copied_input_rejected(self):
        original = save()
        with self.assertRaises(ValueError): v.verify_saves('idle-right', original, original, original, original)

    def test_health_or_extra_field_mutation_rejected(self):
        original = save()
        for change in ('hp', 'extra', 'boolean'):
            altered = fresh(original)
            if change == 'hp': altered['battle']['state']['units'][1]['hp'] += 1
            elif change == 'extra': altered['unexpected'] = True
            else: altered['campaign']['state']['sentinel'] = 1
            with self.assertRaises(ValueError): v.verify_saves('idle-right', original, altered, original, original)

    def test_heal_save_requires39_to69(self):
        original = save(); expected = copy.deepcopy(original)
        expected['battle']['state']['units'][0]['facing'] = 'west'
        expected['battle']['state']['units'][1]['hp'] = 69
        v.verify_saves('recovery-left', original, fresh(expected), original, expected)
        expected['battle']['state']['units'][1]['hp'] = 68
        with self.assertRaises(ValueError): v.verify_saves('recovery-left', original, fresh(expected), original, expected)


if __name__ == '__main__':
    unittest.main()
