#!/usr/bin/env python3
"""Eight isolated comparator controls. Synthetic logs are not native gate evidence."""
import contextlib
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('compare_gate', HERE / 'compare-gate.py')
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


class ComparisonControls(unittest.TestCase):
    def run_control(self, *, mutate_digest=False, terminal=True, finished=True, exit_code="0", uid=213, source=None, hashes=None):
        baseline = (comparison.BASELINE / 'full-native-staged.txt').read_text()
        synthetic = baseline.replace('211 unique script UIDs', f'{uid} unique script UIDs')
        if mutate_digest:
            first_digest = comparison.TRACE.findall(synthetic)[0][2]
            synthetic = synthetic.replace(first_digest, '0' * 64)
        if terminal:
            synthetic += '\n' + comparison.TERMINAL + '\n'
        with tempfile.TemporaryDirectory(prefix='compare-gate-control-') as temporary:
            directory = Path(temporary)
            log = directory / 'synthetic.txt'
            exit_path = directory / 'synthetic.exit'
            log.write_text(synthetic)
            if finished:
                exit_path.write_text(exit_code + '\n')
            extra = []
            for flag, content in [('--source', source), ('--hashes', hashes)]:
                if content is not None:
                    path = directory / flag.removeprefix('--')
                    path.write_text(content)
                    extra.extend([flag, str(path)])
            with contextlib.redirect_stdout(io.StringIO()):
                return comparison.compare(['--log', str(log), '--exit', str(exit_path)] + extra)

    def test_equivalent_complete_fixture(self):
        self.assertEqual(self.run_control(), 0)

    def test_changed_trace_digest_rejected(self):
        self.assertEqual(self.run_control(mutate_digest=True), 1)

    def test_missing_exit_remains_pending(self):
        self.assertEqual(self.run_control(finished=False), 2)

    def test_nonzero_exit_rejected(self):
        self.assertEqual(self.run_control(exit_code='1'), 1)

    def test_old_uid_count_rejected(self):
        self.assertEqual(self.run_control(uid=212), 1)

    def test_wrong_tested_source_rejected(self):
        self.assertEqual(self.run_control(source='90952ac7\n'), 1)

    def test_wrong_assembly_record_rejected(self):
        self.assertEqual(self.run_control(hashes='0' * 64 + '  Empire.dll\n'), 1)

    def test_missing_terminal_rejected(self):
        self.assertEqual(self.run_control(terminal=False), 1)


if __name__ == '__main__':
    unittest.main()
