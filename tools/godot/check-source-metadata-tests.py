#!/usr/bin/env python3
"""Exercise the metadata gate against an isolated Git index, including negative controls."""
from pathlib import Path
import shutil
import subprocess
import tempfile

checker = Path(__file__).with_name("check-source-metadata.py")
with tempfile.TemporaryDirectory(prefix="empire-metadata-checks-") as directory:
    root = Path(directory)
    (root / "tools/godot").mkdir(parents=True)
    (root / "godot").mkdir()
    shutil.copyfile(checker, root / "tools/godot/check-source-metadata.py")
    subprocess.run(["git", "init", "--quiet", str(root)], check=True)

    def put(name, text):
        (root / name).write_text(text)

    def check(expected, diagnostic=""):
        subprocess.run(["git", "add", "--all"], cwd=root, check=True)
        result = subprocess.run(["python3", "tools/godot/check-source-metadata.py"],
                                cwd=root, capture_output=True, text=True, timeout=20)
        if (result.returncode == 0) != expected or diagnostic not in result.stderr:
            raise AssertionError(result.stdout + result.stderr)

    put("godot/Actor.cs", "class Actor {}\n")
    check(False, "Script UID is not indexed")
    put("godot/Actor.cs.uid", "uid://actor123\n")
    check(True)
    put("godot/Other.cs", "class Other {}\n")
    put("godot/Other.cs.uid", "uid://actor123\n")
    check(False, "Duplicate UID")
    put("godot/Other.cs.uid", "uid://other123\n")
    check(True)
    put("godot/Orphan.cs.uid", "uid://orphan123\n")
    check(False, "UID has no indexed source")
    (root / "godot/Orphan.cs.uid").unlink()
    put("godot/icon.png", "fixture image bytes\n")
    check(False, "Import settings are not indexed")
    put("godot/icon.png.import", "[remap]\n")
    check(True)
    put("godot/missing.png.import", "[remap]\n")
    check(False, "Import settings have no indexed source")
    (root / "godot/missing.png.import").unlink()
    put("godot/Actor.cs.uid", "not a uid\n")
    check(False, "Invalid UID")
print("PASS metadata gate: 9 isolated positive and negative cases.")
