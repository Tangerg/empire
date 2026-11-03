#!/usr/bin/env python3
"""Exercise source selection using isolated Git indices and evaluation-only MSBuild projects."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


TOOLS = Path(__file__).resolve().parent
DOTNET = Path(os.environ["DOTNET_ROOT"]) / "dotnet"
ARCHIVE = "godot/docs/audit/Probe.cs"
NESTED_ARCHIVE = "godot/docs/audit/nested/Deep.cs"
REMOVAL = "Empire.Core/**/*.cs;Empire.Core.Tests/**/*.cs;docs/**/*.cs"


class SourceSelectionTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="empire-format-selection-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.environment = os.environ.copy()
        self.environment["EMPIRE_CHECK_TIMEOUT_SECONDS"] = "30"
        self.put("tools/godot/env.sh", 'export DOTNET_ROOT="${DOTNET_ROOT:?}"\n')
        for name in ("format-native.sh", "select-native-format-sources.py"):
            shutil.copyfile(TOOLS / name, self.root / "tools/godot" / name)
        self.git("init", "--quiet")
        self.put(".gitignore", "**/obj/\n**/bin/\n")
        self.main_project()
        self.project("godot/Empire.Core/Empire.Core.csproj")
        self.project("godot/Empire.Core.Tests/Empire.Core.Tests.csproj")
        self.project("tools/godot/checks/Checks.csproj")
        self.active = {
            f"{directory}/Tracked.cs" for directory in (
                "godot/Presentation", "godot/Empire.Core", "godot/Empire.Core.Tests",
                "tools/godot/checks")
        }
        for name in self.active:
            self.put(name, "class Tracked {}\n")
            self.put(str(Path(name).parent / ".gdignore"), "")
        self.put("godot/docs/audit/.gdignore", "")
        self.put(ARCHIVE, "class Archived {}\n")
        self.put(NESTED_ARCHIVE, "class Deep {}\n")
        # This project must not be evaluated merely because it is historical evidence.
        self.put("godot/docs/audit/Probe.csproj",
                 '<Project><Import Project="absent-archive.targets" /></Project>')
        self.git("add", "--all")
        for name in tuple(self.active):
            untracked = name.replace("Tracked.cs", "Untracked.cs")
            self.put(untracked, "class Untracked {}\n")
            self.active.add(untracked)

    def put(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)
        return path

    def git(self, *arguments, input=None):
        return subprocess.check_output(["git", *arguments], cwd=self.root, input=input,
                                       stderr=subprocess.PIPE)

    def project(self, name, extra=""):
        self.put(name, '<Project DefaultTargets="Build"><ItemGroup>'
                 '<Compile Include="**/*.cs" /></ItemGroup>' + extra
                 + '<Target Name="Build"><Error Text="Must never build fixtures" />'
                   '</Target></Project>')

    def main_project(self, removal=REMOVAL, extra="", condition=""):
        self.project("godot/Empire.csproj", f'<ItemGroup{condition}>'
                     f'<Compile Remove="{removal}" /></ItemGroup>'
                     '<ItemGroup><ProjectReference Include="Empire.Core/Empire.Core.csproj" />'
                     '</ItemGroup>' + extra)

    def run_selector(self):
        return subprocess.run(["python3", "tools/godot/select-native-format-sources.py"],
                              cwd=self.root, env=self.environment, capture_output=True, timeout=40)

    def selection(self):
        result = self.run_selector()
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        self.assertTrue(result.stdout.endswith(b"\0"))
        names = [os.fsdecode(name) for name in result.stdout[:-1].split(b"\0")]
        self.assertEqual(len(names), len(set(names)), "A source was selected more than once")
        return set(names)

    def fake_dotnet(self, evaluation=None):
        body = (
            "#!/usr/bin/env python3\nimport json, os, pathlib, sys\n"
            "if sys.argv[1] == 'format':\n"
            "    pathlib.Path('format-arguments.json').write_text(json.dumps(sys.argv[1:]))\n"
            "    sys.exit(0)\n"
        )
        body += evaluation if evaluation is not None else (
            f"os.execv({str(DOTNET)!r}, [{str(DOTNET)!r}, *sys.argv[1:]])\n")
        path = self.put("fake-dotnet/dotnet", body)
        path.chmod(0o755)
        self.environment["DOTNET_ROOT"] = str(path.parent)

    def test_active_sources_survive_all_non_documentation_ignore_markers(self):
        self.assertEqual(self.selection(), self.active)

    def test_untracked_archive_sources_follow_the_same_proven_boundary(self):
        self.put("godot/docs/audit/NewProbe.cs", "class NewProbe {}\n")
        self.assertEqual(self.selection(), self.active)

    def test_documentation_without_an_indexed_marker_is_selected(self):
        self.git("rm", "--cached", "godot/docs/audit/.gdignore")
        self.assertEqual(self.selection(), self.active | {ARCHIVE, NESTED_ARCHIVE})

    def test_deleted_indexed_marker_cannot_hide_sources(self):
        (self.root / "godot/docs/audit/.gdignore").unlink()
        self.assertEqual(self.selection(), self.active | {ARCHIVE, NESTED_ARCHIVE})

    def test_documentation_outside_archive_markers_is_selected(self):
        name = "godot/docs/Example.cs"
        self.put(name, "class Example {}\n")
        self.assertEqual(self.selection(), self.active | {name})

    def test_removing_compile_exclusion_keeps_archive_sources(self):
        self.main_project(removal="Empire.Core/**/*.cs;Empire.Core.Tests/**/*.cs")
        self.assertEqual(self.selection(), self.active | {ARCHIVE, NESTED_ARCHIVE})

    def test_conditional_compile_exclusion_cannot_justify_omission(self):
        self.main_project(condition=' Condition="true"')
        self.assertEqual(self.selection(), self.active | {ARCHIVE, NESTED_ARCHIVE})

    def test_conditional_compile_item_cannot_justify_omission(self):
        self.main_project()
        project = self.root / "godot/Empire.csproj"
        project.write_text(project.read_text().replace('<Compile Remove=', '<Compile Condition="true" Remove='))
        self.assertEqual(self.selection(), self.active | {ARCHIVE, NESTED_ARCHIVE})

    def test_main_compile_reinclusion_is_selected(self):
        self.main_project(extra='<ItemGroup><Compile Include="docs/audit/Probe.cs" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE})

    def test_imported_compile_reinclusion_is_selected(self):
        self.put("godot/reinclude.targets", '<Project><ItemGroup>'
                 '<Compile Include="docs/audit/Probe.cs" /></ItemGroup></Project>')
        self.main_project(extra='<Import Project="reinclude.targets" />')
        self.assertEqual(self.selection(), self.active | {ARCHIVE})

    def test_core_and_tests_can_reinclude_archived_sources(self):
        for project in ("godot/Empire.Core/Empire.Core.csproj",
                        "godot/Empire.Core.Tests/Empire.Core.Tests.csproj"):
            with self.subTest(project=project):
                self.project(project, '<ItemGroup>'
                             '<Compile Include="../docs/audit/Probe.cs" /></ItemGroup>')
                self.assertEqual(self.selection(), self.active | {ARCHIVE})
                self.project(project)

    def test_tools_can_reinclude_archived_sources(self):
        self.project("tools/godot/checks/Checks.csproj", '<ItemGroup>'
                     '<Compile Include="../../../godot/docs/audit/Probe.cs" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE})

    def test_untracked_project_can_reinclude_archived_sources(self):
        self.project("tools/godot/new-checks/NewChecks.csproj", '<ItemGroup>'
                     '<Compile Include="../../../godot/docs/audit/Probe.cs" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE})

    def test_referenced_archived_project_is_evaluated(self):
        self.project("godot/docs/audit/Probe.csproj")
        self.main_project(extra='<ItemGroup>'
                          '<ProjectReference Include="docs/audit/Probe.csproj" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE, NESTED_ARCHIVE})

    def test_transitive_project_reference_is_evaluated(self):
        self.project("godot/docs/audit/Probe.csproj")
        self.project("godot/Empire.Core/Empire.Core.csproj", '<ItemGroup>'
                     '<ProjectReference Include="../docs/audit/Probe.csproj" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE, NESTED_ARCHIVE})

    def test_export_only_compile_reinclusion_is_selected(self):
        self.main_project(extra='<PropertyGroup><Configuration>Debug</Configuration>'
                          '<Configurations>Debug;ExportRelease</Configurations></PropertyGroup>'
                          '<ItemGroup Condition="\'$(Configuration)\' == \'ExportRelease\'">'
                          '<Compile Include="docs/audit/Probe.cs" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE})

    def test_export_only_project_reference_preserves_inherited_configuration(self):
        self.project("godot/docs/audit/Probe.csproj", '<ItemGroup><Compile Remove="**/*.cs" />'
                     '</ItemGroup><ItemGroup Condition="\'$(Configuration)\' == \'ExportRelease\'">'
                     '<Compile Include="Probe.cs" /></ItemGroup>')
        self.main_project(extra='<PropertyGroup><Configuration>Debug</Configuration>'
                          '<Configurations>Debug;ExportRelease</Configurations></PropertyGroup>'
                          '<ItemGroup Condition="\'$(Configuration)\' == \'ExportRelease\'">'
                          '<ProjectReference Include="docs/audit/Probe.csproj" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE})

    def test_tool_harness_root_property_can_reinclude_documentation(self):
        self.project("tools/godot/checks/Checks.csproj", '<ItemGroup>'
                     '<Compile Include="$(EmpireRoot)/godot/docs/audit/Probe.cs" /></ItemGroup>')
        self.assertEqual(self.selection(), self.active | {ARCHIVE})

    def test_unhandled_multitarget_project_fails_closed(self):
        self.project("tools/godot/checks/Checks.csproj", '<PropertyGroup>'
                     '<TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup>')
        result = self.run_selector()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, b"")
        self.assertIn(b"Multi-target project needs archive-boundary review", result.stderr)

    def test_unhandled_project_reference_override_fails_closed(self):
        self.main_project(extra='<ItemGroup><ProjectReference '
                          'Include="Empire.Core.Tests/Empire.Core.Tests.csproj">'
                          '<AdditionalProperties>Configuration=Special</AdditionalProperties>'
                          '</ProjectReference></ItemGroup>')
        result = self.run_selector()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, b"")
        self.assertIn(b"Project-reference property overrides need archive-boundary review", result.stderr)

    def test_missing_files_and_ignored_generated_sources_are_not_selected(self):
        missing = "godot/Presentation/Tracked.cs"
        (self.root / missing).unlink()
        self.put("godot/obj/Generated.cs", "class Generated {}\n")
        self.put("tools/godot/checks/bin/Generated.cs", "class Generated {}\n")
        self.assertEqual(self.selection(), self.active - {missing})

    def test_nul_output_preserves_spaces_newlines_and_non_ascii_names(self):
        names = {"godot/Presentation/A file.cs", "tools/godot/checks/Line\nbreak.cs",
                 "godot/Empire.Core/名字.cs"}
        for name in names:
            self.put(name, "class Name {}\n")
        self.assertEqual(self.selection(), self.active | names)

    def test_duplicate_index_stages_select_a_file_once(self):
        name = "godot/Presentation/Tracked.cs"
        blob = self.git("hash-object", "-w", "--stdin", input=b"class Tracked {}\n").strip()
        self.git("update-index", "--force-remove", name)
        stages = b"".join(b"100644 " + blob + f" {stage}\t{name}\n".encode()
                          for stage in (1, 2, 3))
        self.git("update-index", "--index-info", input=stages)
        self.assertEqual(self.selection(), self.active)

    def test_invalid_msbuild_output_fails_without_partial_selection(self):
        properties = {"Configurations": "", "Configuration": "", "TargetFrameworks": ""}
        cases = [
            ("not JSON", "Expecting value"),
            (json.dumps({"Properties": properties, "Items": {"Compile": []}}), "ProjectReference"),
            (json.dumps({"Properties": properties, "Items": {
                "Compile": [{"FullPath": "relative.cs"}], "ProjectReference": []}}), "Non-absolute evaluated Compile"),
            (json.dumps({"Properties": properties, "Items": {
                "Compile": {}, "ProjectReference": []}}), "Invalid evaluated Compile list"),
        ]
        for output, diagnostic in cases:
            with self.subTest(diagnostic=diagnostic):
                self.fake_dotnet(f"print({output!r})\n")
                result = self.run_selector()
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stdout, b"")
                self.assertIn(b"Native format source selection failed", result.stderr)
                self.assertIn(diagnostic.encode(), result.stderr)

    def test_msbuild_failure_stops_formatter(self):
        self.fake_dotnet("sys.exit(23)\n")
        result = subprocess.run(["bash", "tools/godot/format-native.sh", "--check"],
                                cwd=self.root, env=self.environment, capture_output=True, timeout=40)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"Native format source selection failed", result.stderr)
        self.assertFalse((self.root / "format-arguments.json").exists())

    def test_git_failure_stops_formatter(self):
        self.fake_dotnet()
        (self.root / ".git").rename(self.root / ".unavailable-git")
        result = subprocess.run(["bash", "tools/godot/format-native.sh", "--check"],
                                cwd=self.root, env=self.environment, capture_output=True, timeout=40)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"Native format source selection failed", result.stderr)
        self.assertFalse((self.root / "format-arguments.json").exists())

    def test_selector_timeout_stops_formatter(self):
        self.fake_dotnet("import time\ntime.sleep(10)\n")
        self.environment["EMPIRE_CHECK_TIMEOUT_SECONDS"] = "0.2"
        result = subprocess.run(["bash", "tools/godot/format-native.sh", "--check"],
                                cwd=self.root, env=self.environment, capture_output=True, timeout=5)
        self.assertEqual(result.returncode, 124)
        self.assertFalse((self.root / "format-arguments.json").exists())

    def test_empty_source_set_fails_instead_of_formatting_everything(self):
        for name in self.active:
            (self.root / name).unlink()
        result = self.run_selector()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, b"")
        self.assertIn(b"No native source files found", result.stderr)

    def test_formatter_receives_each_complete_filename_and_correct_mode(self):
        name = "godot/Presentation/A file\n名字.cs"
        self.put(name, "class Name {}\n")
        self.fake_dotnet()
        for mode in ("--check", "--write"):
            with self.subTest(mode=mode):
                result = subprocess.run(["bash", "tools/godot/format-native.sh", mode],
                                        cwd=self.root, env=self.environment, capture_output=True, timeout=40)
                self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
                arguments = json.loads((self.root / "format-arguments.json").read_text())
                self.assertEqual(arguments[:5], ["format", "whitespace", "--folder", ".", "--include"])
                expected = sorted(self.active | {name})
                if mode == "--check":
                    expected.append("--verify-no-changes")
                self.assertEqual(arguments[5:], expected)

    def test_real_formatter_checks_active_debt_and_preserves_archive_bytes(self):
        for name in self.active:
            self.put(name, "class Active\n{\n}\n")
        self.put(ARCHIVE, "class Archived{public int Value=>  2;}\n")
        archived_bytes = {name: (self.root / name).read_bytes()
                          for name in (ARCHIVE, NESTED_ARCHIVE)}

        def format_sources(mode):
            return subprocess.run(["bash", "tools/godot/format-native.sh", mode],
                                  cwd=self.root, env=self.environment, capture_output=True, timeout=40)

        result = format_sources("--check")
        self.assertEqual(result.returncode, 0, (result.stdout + result.stderr).decode(errors="replace"))
        active = self.put("godot/Presentation/Tracked.cs", "class Active{public int Value=>1;}\n")
        malformed = active.read_bytes()
        result = format_sources("--check")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"WHITESPACE", result.stdout + result.stderr)
        self.assertIn(b"Tracked.cs", result.stdout + result.stderr)
        self.assertEqual(active.read_bytes(), malformed, "Check mode changed active source")
        result = format_sources("--write")
        self.assertEqual(result.returncode, 0, (result.stdout + result.stderr).decode(errors="replace"))
        self.assertNotEqual(active.read_bytes(), malformed)
        result = format_sources("--check")
        self.assertEqual(result.returncode, 0, (result.stdout + result.stderr).decode(errors="replace"))
        for name, original in archived_bytes.items():
            self.assertEqual((self.root / name).read_bytes(), original, "Formatter changed archived evidence")


if __name__ == "__main__":
    unittest.main()
