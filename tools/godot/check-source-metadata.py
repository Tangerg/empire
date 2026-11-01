#!/usr/bin/env python3
"""Check indexed Godot source companions; never inspect or add ignored caches."""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
tracked = set(subprocess.check_output(
    ["git", "ls-files", "-z", "--", "godot"], cwd=root
).decode().split("\0")) - {""}
# Godot deliberately does not import resources beneath an indexed .gdignore.
# An untracked marker must not hide missing companions in the committed source set.
ignored_directories = {Path(name).parent for name in tracked if name.endswith("/.gdignore")}
errors = []
uids = {}
ignored = 0
for name in sorted(tracked):
    path = root / name
    if any(Path(name).is_relative_to(directory) and Path(name) != directory / ".gdignore"
           for directory in ignored_directories):
        ignored += 1
        continue
    if not path.exists():
        errors.append(f"Indexed source is absent: {name}")
        continue
    if name.endswith(".uid"):
        source = name[:-4]
        if source not in tracked:
            errors.append(f"UID has no indexed source: {name}")
        value = path.read_text().strip()
        if not re.fullmatch(r"uid://[a-z0-9]+", value):
            errors.append(f"Invalid UID: {name}")
        elif value in uids:
            errors.append(f"Duplicate UID: {name} and {uids[value]}")
        else:
            uids[value] = name
    elif name.endswith(".import"):
        if name[:-7] not in tracked:
            errors.append(f"Import settings have no indexed source: {name}")
    elif path.suffix in {".cs", ".gd"}:
        if name + ".uid" not in tracked:
            errors.append(f"Script UID is not indexed: {name}.uid")
    elif path.suffix in {".png", ".svg", ".ttf", ".otf", ".jpg", ".jpeg", ".webp"}:
        if name + ".import" not in tracked:
            errors.append(f"Import settings are not indexed: {name}.import")
if errors:
    print("\n".join(errors), file=sys.stderr)
    sys.exit(1)
print(f"Godot source metadata: {len(uids)} unique script UIDs; indexed resource companions present; {ignored} intentionally unimported audit files.")
