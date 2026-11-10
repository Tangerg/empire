#!/usr/bin/env python3
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


spec = importlib.util.spec_from_file_location("audit", Path(__file__).with_name("audit-staged-recovery.py"))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class AdmissionChecks(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="empire-staged-admission-test-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.source = {"commit": "isolated-test"}
        plan = {"source": self.source, "stages": [{"name": "first", "command": "printf first"},
                                                   {"name": "second", "command": "printf second"}]}
        (self.root / "plan.json").write_text(json.dumps(plan))
        self.stage = self.root / "01-first"
        self.stage.mkdir()
        (self.stage / "attempt-01.log").write_bytes(b"first\n")
        self.result = {"source": self.source, "command": "printf first", "terminal": True,
                       "exitCode": 0, "log": "attempt-01.log",
                       "logSha256": hashlib.sha256(b"first\n").hexdigest()}

    def check(self):
        (self.stage / "result.json").write_text(json.dumps(self.result))
        return audit.validate_records(self.root)

    def test_valid_prefix_is_not_a_full_gate_count(self):
        self.assertEqual(self.check(), 1)

    def test_wrong_command_rejected(self):
        self.result["command"] = "printf second"
        with self.assertRaisesRegex(ValueError, "command differs"):
            self.check()

    def test_nonterminal_result_rejected(self):
        self.result["terminal"] = False
        with self.assertRaisesRegex(ValueError, "successful terminal"):
            self.check()

    def test_nonzero_exit_rejected(self):
        self.result["exitCode"] = 3
        with self.assertRaisesRegex(ValueError, "successful terminal"):
            self.check()

    def test_wrong_source_rejected(self):
        self.result["source"] = {"commit": "different"}
        with self.assertRaisesRegex(ValueError, "source differs"):
            self.check()

    def test_wrong_log_rejected(self):
        (self.stage / "attempt-01.log").write_bytes(b"second\n")
        with self.assertRaisesRegex(ValueError, "log changed"):
            self.check()

    def test_gap_rejected(self):
        second = self.root / "02-second"
        second.mkdir()
        (second / "result.json").write_text(json.dumps(self.result))
        with self.assertRaisesRegex(ValueError, "contiguous prefix"):
            audit.validate_records(self.root)


if __name__ == "__main__":
    unittest.main()
