"""Read-only integrity and state checks for the recorded native GUI pass."""

import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parent


def saved(name):
    value = json.loads((ROOT / "saves" / f"{name}.json").read_text())
    # Saving is intentionally time-stamped. No gameplay field is excluded.
    del value["campaign"]["savedAt"]
    del value["battle"]["savedAt"]
    return value


def human_units(value):
    return [unit for unit in value["battle"]["state"]["units"] if unit["owner"] == 1]


before = saved("before-navigation")
assert before == saved("after-navigation"), "Navigation changed canonical state/events"
assert before == saved("after-undo"), "Undo did not restore the original state/events"
wait = saved("after-wait")
assert [unit["id"] for unit in human_units(wait) if unit["done"]] == [5]
zero = saved("zero-ready")
assert len(human_units(zero)) == 11 and all(unit["done"] for unit in human_units(zero))
assert zero["battle"]["state"]["turn"] == 1
assert zero["battle"]["state"]["currentPlayer"] == 1
second = saved("round-two")
assert len(human_units(second)) == 11 and not any(unit["done"] for unit in human_units(second))
assert second["battle"]["state"]["turn"] == 2
assert second["battle"]["state"]["currentPlayer"] == 1
assert len(second["events"]) == 16
for path in sorted((ROOT / "screenshots").glob("*.png")):
    raw = path.read_bytes()
    assert raw[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", raw[16:24]) == (960, 640)
manifest = json.loads((ROOT / "manifest.json").read_text())
for entry in manifest["files"]:
    raw = (ROOT / entry["path"]).read_bytes()
    assert len(raw) == entry["bytes"]
    assert hashlib.sha256(raw).hexdigest() == entry["sha256"], entry["path"]
print("PASS: navigation/Undo state equality, Wait/zero/turn-2 states, PNG dimensions, and manifest")
