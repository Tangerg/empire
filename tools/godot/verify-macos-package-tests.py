#!/usr/bin/env python3
"""Small mutation checks for the static archive guard, not a macOS runtime test."""
import importlib.util
import json
from pathlib import Path
import plistlib
import struct
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("verify_macos", Path(__file__).with_name("verify-macos-package.py"))
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


def macho(cpu, signed=True):
    code_directory = struct.pack(">IIII", 0xFADE0C02, 16, 0x20001, 2)
    signature = struct.pack(">IIIII", 0xFADE0CC0, 36, 1, 0, 20) + code_directory
    header = struct.pack("<IIIIIIII", 0xFEEDFACF, cpu, 0, 2, int(signed), 16 if signed else 0, 0, 0)
    return header + (struct.pack("<IIII", 0x1D, 16, 48, len(signature)) + signature if signed else b"")


def universal():
    x64, arm = macho(0x01000007), macho(0x0100000C)
    return (struct.pack(">II", 0xCAFEBABE, 2)
            + struct.pack(">IIIII", 0x01000007, 0, 48, len(x64), 0)
            + struct.pack(">IIIII", 0x0100000C, 0, 48 + len(x64), len(arm), 0) + x64 + arm)


def fixture():
    root = "Test.app/Contents/"
    files = {root + "Info.plist": plistlib.dumps({"CFBundleIdentifier": "org.empire.grayflag.migration", "CFBundleExecutable": "Test"}),
             root + "MacOS/Test": universal(), root + "_CodeSignature/CodeResources": b"fixture",
             root + "Resources/Test.pck": b"fixture", "README-UNTESTED.md": b"NOT TESTED"}
    for arch, cpu in (("x86_64", 0x01000007), ("arm64", 0x0100000C)):
        prefix = root + "Resources/data_Empire_macos_" + arch + "/"
        for name in ("Empire.dll", "Empire.Core.dll", "GodotSharp.dll"):
            files[prefix + name] = b"fixture"
        for name in ("libcoreclr.dylib", "libhostfxr.dylib", "libhostpolicy.dylib"):
            files[prefix + name] = macho(cpu)
        files[prefix + "Empire.runtimeconfig.json"] = json.dumps({"runtimeOptions": {"includedFrameworks": [{"name": "Microsoft.NETCore.App", "version": "8.0.32"}]}}).encode()
    return files


class ArchiveChecks(unittest.TestCase):
    def inspect(self, files, executable=True):
        with tempfile.TemporaryDirectory(prefix="empire-macos-guard-") as directory:
            path = Path(directory) / "fixture.zip"
            with zipfile.ZipFile(path, "w") as archive:
                for name, value in files.items():
                    item = zipfile.ZipInfo(name)
                    item.external_attr = (0o100755 if executable and "/MacOS/" in name else 0o100644) << 16
                    archive.writestr(item, value)
            return verifier.inspect_archive(path)

    def test_valid_structure_does_not_claim_runtime_success(self):
        report = self.inspect(fixture())
        self.assertEqual("NOT TESTED", report["macos_execution"])
        self.assertEqual("NOT RUN", report["macos_codesign_verification"])

    def test_truncated_universal_executable_is_refused(self):
        with self.assertRaisesRegex(ValueError, "Mach-O slice is outside file"):
            verifier.inspect_macho(universal()[:80])

    def test_missing_arm_runtime_is_refused(self):
        files = fixture()
        del files["Test.app/Contents/Resources/data_Empire_macos_arm64/libcoreclr.dylib"]
        with self.assertRaisesRegex(ValueError, "Missing"):
            self.inspect(files)

    def test_wrong_runtime_architecture_is_refused(self):
        files = fixture()
        files["Test.app/Contents/Resources/data_Empire_macos_arm64/libcoreclr.dylib"] = macho(0x01000007)
        with self.assertRaisesRegex(ValueError, "Wrong native runtime architecture"):
            self.inspect(files)

    def test_unsigned_executable_is_refused(self):
        with self.assertRaisesRegex(ValueError, "Missing Mach-O CodeDirectory"):
            verifier.inspect_macho(macho(0x01000007, signed=False))

    def test_missing_executable_mode_is_refused(self):
        with self.assertRaisesRegex(ValueError, "Executable mode missing"):
            self.inspect(fixture(), executable=False)

    def test_archive_traversal_is_refused(self):
        files = fixture()
        files["../outside"] = b"fixture"
        with self.assertRaisesRegex(ValueError, "Unsafe archive path"):
            self.inspect(files)


if __name__ == "__main__":
    unittest.main()
