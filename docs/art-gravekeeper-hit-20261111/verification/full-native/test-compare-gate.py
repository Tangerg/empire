#!/usr/bin/env python3
"""Read-only comparator controls. Synthetic fixtures are never native gate evidence."""
import argparse
import contextlib
import importlib.util
import io
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--reference-root', type=Path)
options, test_args = parser.parse_known_args()
REFERENCE = options.reference_root or next((p for p in HERE.parents if (p / 'godot').is_dir() and (p / 'tools/godot').is_dir()), None)
if REFERENCE is None:
    parser.error('--reference-root is required outside the repository')
spec = importlib.util.spec_from_file_location('compare_gate', HERE / 'compare-gate.py')
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)
# These deliberately synthetic identities make the controls independent of actual
# build pinning. They cannot be used by a normal comparator invocation.
SYNTHETIC_SOURCE = '1' * 40
SYNTHETIC_HASHES = {'Empire.Core.dll': 'a' * 64, 'Empire.dll': 'b' * 64, 'GodotSharp.dll': 'c' * 64}


class ComparisonControls(unittest.TestCase):
    def run_control(self, *, mutate=None, terminal=True, finished=True, exit_code='0', uid=215,
                    source=None, hashes=None, dirty=False, pin=True, missing_log=False):
        baseline = (REFERENCE / 'docs/art-gravekeeper-idle-20261110/verification/full-native/full-native.txt').read_text()
        synthetic = baseline.replace('214 unique script UIDs', f'{uid} unique script UIDs')
        synthetic = synthetic.replace(comparison.TERMINAL, '')
        if mutate:
            changed = mutate(synthetic)
            self.assertTrue(changed != synthetic, 'Control mutation must change its fixture.')
            synthetic = changed
        if terminal:
            synthetic += '\n' + comparison.TERMINAL + '\n'
        with tempfile.TemporaryDirectory(prefix='keeper-hit-comparison-control-') as temporary:
            directory = Path(temporary)
            log = directory / 'synthetic.txt'
            exit_path = directory / 'synthetic.exit'
            source_path = directory / 'source.txt'
            clean_path = directory / 'clean.txt'
            hash_path = directory / 'hashes.txt'
            if not missing_log:
                log.write_text(synthetic)
            if finished:
                exit_path.write_text(exit_code + '\n')
            source_path.write_text(SYNTHETIC_SOURCE + '\n' if source is None else source)
            clean_path.write_text(' M godot/Presentation/BoardView.Impact.cs\n' if dirty else '')
            hash_path.write_text(''.join(f'{value}  historical/absolute/path/{name}\n' for name, value in SYNTHETIC_HASHES.items()) if hashes is None else hashes)
            values = {'SOURCE_COMMIT': SYNTHETIC_SOURCE if pin else None,
                      'GODOT_TREE': '2' * 40 if pin else None, 'TOOLS_TREE': '3' * 40 if pin else None,
                      'EXPECTED_HASHES': SYNTHETIC_HASHES if pin else None}
            with patch.multiple(comparison, **values), contextlib.redirect_stdout(io.StringIO()):
                return comparison.compare(['--reference-root', str(REFERENCE), '--log', str(log), '--exit', str(exit_path),
                                           '--source', str(source_path), '--source-status', str(clean_path), '--hashes', str(hash_path)])

    def test_equivalent_complete_fixture(self):
        self.assertEqual(self.run_control(), 0)

    def test_changed_trace_digest_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s.replace(comparison.TRACE.findall(s)[0][2], '0' * 64)), 1)

    def test_changed_order_count_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s.replace('AI_TRACE c01-01: 156 steps', 'AI_TRACE c01-01: 157 steps')), 1)

    def test_duplicate_trace_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s + '\n' + next(line for line in s.splitlines() if 'AI_TRACE c01-01:' in line)), 1)

    def test_changed_played_outcome_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s.replace(comparison.PLAYED.findall(s)[0], comparison.PLAYED.findall(s)[0] + ' CHANGED')), 1)

    def test_changed_progress_marker_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s.replace(comparison.PROGRESS.findall(s)[0], comparison.PROGRESS.findall(s)[0] + ' CHANGED')), 1)

    def test_missing_exit_remains_pending(self):
        self.assertEqual(self.run_control(finished=False), 2)

    def test_missing_log_and_exit_remains_pending(self):
        self.assertEqual(self.run_control(finished=False, missing_log=True), 2)

    def test_missing_log_with_completed_exit_rejected(self):
        self.assertEqual(self.run_control(missing_log=True), 1)

    def test_nonzero_exit_rejected(self):
        self.assertEqual(self.run_control(exit_code='1'), 1)

    def test_old_uid_count_rejected(self):
        self.assertEqual(self.run_control(uid=214), 1)

    def test_duplicate_uid_marker_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s + '\nGodot source metadata: 215 unique script UIDs; duplicated\n'), 1)

    def test_wrong_tested_source_rejected(self):
        self.assertEqual(self.run_control(source='9' * 40 + '\n'), 1)

    def test_dirty_source_rejected(self):
        self.assertEqual(self.run_control(dirty=True), 1)

    def test_wrong_assembly_record_rejected(self):
        self.assertEqual(self.run_control(hashes='0' * 64 + '  Empire.dll\n'), 1)

    def test_duplicate_assembly_hash_errors(self):
        hashes = 'a' * 64 + '  Empire.dll\n' + 'b' * 64 + '  another/path/Empire.dll\n'
        with self.assertRaisesRegex(ValueError, 'Duplicate assembly hash'):
            self.run_control(hashes=hashes)

    def test_unpinned_build_rejected(self):
        self.assertEqual(self.run_control(pin=False), 1)

    def test_missing_terminal_rejected(self):
        self.assertEqual(self.run_control(terminal=False), 1)

    def test_added_warning_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s + '\nWARNING: new diagnostic\n'), 1)

    def test_missing_warning_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s.replace(next(line for line in s.splitlines() if line.startswith('WARNING:')), '', 1)), 1)

    def test_duplicate_warning_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s + '\n' + next(line for line in s.splitlines() if line.startswith('WARNING:'))), 1)

    def test_error_diagnostic_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s + '\nERROR: test mutation\n'), 1)

    def test_leak_diagnostic_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s + '\nObjectDB instances leaked at exit\n'), 1)

    def test_build_warning_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: s.replace('0 Warning(s)', '1 Warning(s)', 1)), 1)

    def test_missing_stage_marker_rejected(self):
        self.assertEqual(self.run_control(mutate=lambda s: re.sub(r'^Animation presentation checks passed:.*$', '', s, flags=re.M)), 1)

    def test_changed_assertion_total_allowed(self):
        self.assertEqual(self.run_control(mutate=lambda s: s.replace('6876 assertions', '9999 assertions')), 0)


if __name__ == '__main__':
    unittest.main(argv=[__file__] + test_args)
