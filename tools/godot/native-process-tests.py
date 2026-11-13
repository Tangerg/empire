#!/usr/bin/env python3
"""Exercise real gate control flow with isolated, explicitly simulated tool processes."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


TOOLS = Path(__file__).resolve().parent
IMPORT_ERROR = ('ERROR: EditorSettings not instantiated yet when getting setting '
                '"export/android/android_sdk_path".')
SCRIPT_ERROR = "SCRIPT ERROR: Invalid access to property or key 'required_field' on a base object of type 'Nil'."
SUCCESS = "Current native contracts, replay/save determinism, and application flow passed."


class NativeGateTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="empire-native-gate-tests-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.environment = os.environ.copy()
        self.environment.update(EMPIRE_TOOLCHAIN_ROOT=str(self.root / "toolchain"),
                                EMPIRE_CHECK_TIMEOUT_SECONDS="5",
                                EMPIRE_REPLAY_TIMEOUT_SECONDS="5",
                                EMPIRE_CAMPAIGN_TIMEOUT_SECONDS="7",
                                TMPDIR=str(self.root / "temporary"),
                                TRACE=str(self.root / "trace.jsonl"))
        (self.root / "temporary").mkdir()
        self.environment["PATH"] = str(self.root / "bin") + os.pathsep + self.environment["PATH"]
        for name in ("env.sh", "verify-native.sh", "native-process.sh"):
            if (TOOLS / name).exists():
                self.put("tools/godot/" + name, (TOOLS / name).read_text())
        for name in ("format-native.sh", "objective-outcome-checks/run.sh", "catalog-boundary-checks/run.sh"):
            self.put("tools/godot/" + name, "#!/usr/bin/env bash\nexit 0\n")
        for name in ("bin/python3", "bin/node", "toolchain/dotnet/dotnet"):
            self.put(name, "#!/usr/bin/env bash\nexit 0\n", executable=True)
        self.godot = self.put(
            "toolchain/godot/Godot_v4.6.3-stable_mono_linux_x86_64/"
            "Godot_v4.6.3-stable_mono_linux.x86_64",
            f"#!{sys.executable}\n" + r'''
import json, os, pathlib, sys
args = sys.argv[1:]
with open(os.environ["TRACE"], "a") as trace:
    trace.write(json.dumps({"args": args, "data": os.environ.get("XDG_DATA_HOME"),
                           "budget": os.environ.get("EMPIRE_CHECK_TIMEOUT_SECONDS")}) + "\n")
editor = "--editor" in args
mode = os.environ.get("SIMULATED_RESULT", "clean")
if editor and mode == "import-error":
    print(os.environ["IMPORT_ERROR"], file=sys.stderr)
elif editor and mode == "import-script-error":
    print(os.environ["SCRIPT_ERROR"], file=sys.stderr)
elif editor and mode == "import-nonzero":
    print("Import exited unsuccessfully", file=sys.stderr)
    sys.exit(7)
elif not editor and mode == "scene-error":
    print("PASS before teardown")
    print("ERROR: simulated scene teardown", file=sys.stderr)
elif not editor and mode == "scene-script-error":
    print("PASS before teardown")
    print(os.environ["SCRIPT_ERROR"], file=sys.stderr)
else:
    print("Clean simulated process")
''', executable=True)
        self.environment["IMPORT_ERROR"] = IMPORT_ERROR
        self.environment["SCRIPT_ERROR"] = SCRIPT_ERROR

    def put(self, name, content, executable=False):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
        if executable:
            path.chmod(0o755)
        return path

    def aggregate(self, mode):
        self.environment["SIMULATED_RESULT"] = mode
        result = subprocess.run(["bash", "tools/godot/verify-native.sh"], cwd=self.root,
                                env=self.environment, text=True, capture_output=True, timeout=15)
        trace = self.root / "trace.jsonl"
        calls = [json.loads(line) for line in trace.read_text().splitlines()] if trace.exists() else []
        return result, calls

    def test_zero_exit_editor_error_stops_before_native_scenes(self):
        result, calls = self.aggregate("import-error")
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(len(calls), 1)
        self.assertIn("--editor", calls[0]["args"])
        self.assertIn(IMPORT_ERROR, result.stdout + result.stderr)
        self.assertNotIn(SUCCESS, result.stdout)
        logs = list((self.root / "temporary").rglob("*.log"))
        self.assertTrue(any(IMPORT_ERROR in log.read_text() for log in logs))

    def test_editor_nonzero_stops_without_success(self):
        result, calls = self.aggregate("import-nonzero")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(len(calls), 1)
        self.assertNotIn(SUCCESS, result.stdout)

    def test_zero_exit_script_error_stops_import_before_native_scenes(self):
        result, calls = self.aggregate("import-script-error")
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(len(calls), 1)
        self.assertIn("--editor", calls[0]["args"])
        self.assertIn(SCRIPT_ERROR, result.stdout + result.stderr)
        self.assertNotIn(SUCCESS, result.stdout)

    def test_zero_exit_script_error_stops_scene_after_success_marker(self):
        result, calls = self.aggregate("scene-script-error")
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(len(calls), 2)
        self.assertIn("res://Scenes/NarrativeChecks.tscn", calls[1]["args"])
        self.assertIn("PASS before teardown", result.stdout)
        self.assertIn(SCRIPT_ERROR, result.stdout + result.stderr)
        self.assertNotIn(SUCCESS, result.stdout)

    def test_scene_error_after_pass_stops_at_first_scene(self):
        result, calls = self.aggregate("scene-error")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(len(calls), 2)
        self.assertIn("res://Scenes/NarrativeChecks.tscn", calls[1]["args"])
        self.assertIn("PASS before teardown", result.stdout)
        self.assertIn("ERROR: simulated scene teardown", result.stdout + result.stderr)
        self.assertNotIn(SUCCESS, result.stdout)

    def test_clean_gate_keeps_scene_arguments_and_isolated_save_namespaces(self):
        result, calls = self.aggregate("clean")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn(SUCCESS, result.stdout)
        self.assertEqual(len(calls), 42)
        self.assertIn("--editor", calls[0]["args"])
        scenes = calls[1:]
        self.assertEqual(len({call["data"] for call in scenes}), 41)
        self.assertTrue(all(Path(call["data"]).is_dir() for call in scenes))
        self.assertNotIn(calls[0]["data"], {call["data"] for call in scenes})
        by_scene = {next(arg for arg in call["args"] if arg.endswith(".tscn")): call for call in scenes}
        self.assertEqual(len(by_scene), 41)
        for scene in ("GrainRoadChecks", "LedgerHintChecks", "C09CandidateChecks", "C10CandidateChecks", "C11CandidateChecks", "C12CandidateChecks", "ArchiveDistrictChecks", "ForgeDistrictChecks", "C15CandidateChecks"):
            self.assertEqual(by_scene[f"res://Scenes/{scene}.tscn"]["args"][-1], f"res://Scenes/{scene}.tscn")
        self.assertEqual(by_scene["res://Scenes/ReadyUnitNavigationChecks.tscn"]["args"][-1],
                         "res://Scenes/ReadyUnitNavigationChecks.tscn")
        self.assertEqual(by_scene["res://Scenes/LightingChecks.tscn"]["args"][-2:],
                         ["--", f"--lighting-oracle={self.root}/godot/tests/oracle/lighting.json"])
        self.assertEqual(by_scene["res://Scenes/SceneryChecks.tscn"]["args"][-2:],
                         ["--", "--scenery-oracle=res://tests/oracle/board-scenery.json.gz"])
        campaign = by_scene["res://Scenes/CampaignPlayedFlowChecks.tscn"]
        self.assertEqual(campaign["args"][-1], "res://Scenes/CampaignPlayedFlowChecks.tscn")
        self.assertEqual(campaign["budget"], "7")
        self.assertTrue(all(call["budget"] == "5" for call in calls if call is not campaign))

    def test_default_campaign_budget_is_separate_from_individual_scenes(self):
        self.environment.pop("EMPIRE_CHECK_TIMEOUT_SECONDS")
        self.environment.pop("EMPIRE_CAMPAIGN_TIMEOUT_SECONDS")
        result, calls = self.aggregate("clean")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(len(calls), 42)
        self.assertIn("res://Scenes/CampaignPlayedFlowChecks.tscn", calls[-1]["args"])
        self.assertEqual(calls[-1]["budget"], "2400")
        self.assertTrue(all(call["budget"] == "900" for call in calls[:-1]))


class NativeProcessTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="empire-process-tests-")
        self.addCleanup(self.directory.cleanup)
        self.log = Path(self.directory.name) / "process with spaces.log"
        self.environment = os.environ.copy()
        self.environment["EMPIRE_CHECK_TIMEOUT_SECONDS"] = "5"

    def run_process(self, command):
        return subprocess.run(["bash", str(TOOLS / "native-process.sh"), str(self.log),
                               "fixture process", *command], env=self.environment,
                              text=True, capture_output=True, timeout=10)

    def test_clean_warning_and_arguments_are_preserved(self):
        arguments = ["with spaces", "quotation'\"", "中文", "--flag=value with spaces"]
        result = self.run_process([sys.executable, "-c",
                                   "import json,sys; print(json.dumps(sys.argv[1:])); "
                                   "print('WARNING: explicit diagnostic mentioning SCRIPT ERROR: as text', file=sys.stderr)", *arguments])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout, self.log.read_text())
        self.assertIn(json.dumps(arguments), result.stdout)
        self.assertIn("WARNING: explicit diagnostic", result.stdout)

    def test_zero_exit_diagnostics_are_rejected_on_both_streams(self):
        for diagnostic in (IMPORT_ERROR, SCRIPT_ERROR, "ObjectDB instances leaked at exit.",
                           "Segmentation fault", "Unhandled Exception: fixture"):
            for stream in ("stdout", "stderr"):
                with self.subTest(diagnostic=diagnostic, stream=stream):
                    result = self.run_process([sys.executable, "-c",
                                               "import sys; print('PASS before teardown'); "
                                               f"print({diagnostic!r}, file=sys.{stream})"])
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn(diagnostic, self.log.read_text())
                    self.assertIn(diagnostic, result.stdout)
                    self.assertIn("exit 0", result.stderr)
                    self.assertIn(str(self.log), result.stderr)

    def test_nonzero_without_diagnostic_is_rejected(self):
        result = self.run_process([sys.executable, "-c", "print('PASS before failure'); raise SystemExit(7)"])
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("exit 7", result.stderr)
        self.assertIn("PASS before failure", self.log.read_text())

    def test_timeout_cannot_pass(self):
        self.environment["EMPIRE_CHECK_TIMEOUT_SECONDS"] = "0.1"
        result = self.run_process([sys.executable, "-c", "import time; print('Started', flush=True); time.sleep(3)"])
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("exit 124", result.stderr)
        self.assertIn("Started", self.log.read_text())

    def test_missing_command_cannot_pass(self):
        result = self.run_process([str(Path(self.directory.name) / "missing-command")])
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("exit 127", result.stderr)
        self.assertIn("missing-command", self.log.read_text())

    def test_missing_arguments_are_rejected(self):
        result = subprocess.run(["bash", str(TOOLS / "native-process.sh")],
                                text=True, capture_output=True, timeout=5)
        self.assertEqual(result.returncode, 2)
        self.assertIn("Usage:", result.stderr)


if __name__ == "__main__":
    unittest.main()
