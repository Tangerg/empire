#!/usr/bin/env python3
"""Isolated recovery-runner controls; these are not game acceptance tests."""
from contextlib import redirect_stdout
import fcntl
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("recovery", Path(__file__).with_name("staged-native-recovery.py"))
recovery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(recovery)


class RunnerChecks(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="empire-recovery-runner-test-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.output = self.root / "results"
        self.commands = [(f"stage-{index + 1:02d}", f"printf 'stage {index + 1}\\n'") for index in range(45)]
        setup = {"preflight": "set -euo pipefail\n", "native": "set -euo pipefail\n"}
        for target, value in (("plan_for", lambda _: (setup, self.commands)),
                              ("source_identity", lambda _: {"commit": "isolated-fixture"}),
                              ("environment_identity", lambda _: {"tools": "isolated-fixture"})):
            mock = patch.object(recovery, target, value)
            mock.start()
            self.addCleanup(mock.stop)
        silence = redirect_stdout(io.StringIO())
        silence.__enter__()
        self.addCleanup(silence.__exit__, None, None, None)

    def run_gate(self):
        return recovery.run(self.repo, self.output)

    def test_complete_and_resume_do_not_repeat_completed_stages(self):
        self.assertEqual(self.run_gate(), 0)
        complete = json.loads((self.output / "complete.json").read_text())
        self.assertEqual(complete["stagesPassed"], 45)
        self.assertFalse(complete["singleProcessAggregatePass"])
        with patch.object(recovery.subprocess, "run", side_effect=AssertionError("A completed command repeated")):
            self.assertEqual(self.run_gate(), 0)

    def test_failure_never_writes_aggregate_completion(self):
        self.commands[1] = ("stage-02", "exit 7")
        self.assertEqual(self.run_gate(), 7)
        self.assertFalse((self.output / "complete.json").exists())
        self.assertFalse((self.output / "03-stage-03").exists())
        with self.assertRaisesRegex(RuntimeError, "Failed or stale"):
            self.run_gate()

    def test_incomplete_stage_restarts_but_completed_prefix_is_reused(self):
        actual = subprocess.run
        calls = 0

        def interrupt(*args, **kwargs):
            nonlocal calls
            calls += 1
            if calls == 2:
                raise RuntimeError("simulated interruption before terminal result")
            return actual(*args, **kwargs)

        with patch.object(recovery.subprocess, "run", side_effect=interrupt):
            with self.assertRaisesRegex(RuntimeError, "simulated interruption"):
                self.run_gate()
        self.assertFalse((self.output / "02-stage-02/result.json").exists())
        self.assertEqual(self.run_gate(), 0)
        self.assertEqual(len(list((self.output / "01-stage-01").glob("attempt-*.log"))), 1)
        self.assertEqual(len(list((self.output / "02-stage-02").glob("attempt-*.log"))), 2)

    def test_modified_log_blocks_resume(self):
        self.assertEqual(self.run_gate(), 0)
        (self.output / "01-stage-01/attempt-01.log").write_text("changed")
        with self.assertRaisesRegex(RuntimeError, "Log changed"):
            self.run_gate()

    def test_changed_runtime_artifact_blocks_resume(self):
        self.assertEqual(self.run_gate(), 0)
        imported = self.repo / "godot/.godot/imported"
        imported.mkdir(parents=True)
        (imported / "unexpected.ctex").write_bytes(b"changed runtime input")
        with self.assertRaisesRegex(RuntimeError, "Runtime artifacts changed"):
            self.run_gate()

    def test_existing_lock_blocks_duplicate_runner(self):
        self.output.mkdir()
        with (self.output / "runner.lock").open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            with self.assertRaisesRegex(RuntimeError, "still holds the execution lock"):
                self.run_gate()


if __name__ == "__main__":
    unittest.main()
