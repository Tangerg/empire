"""Read the shipped PO catalog for the same pure presentation factories used by Godot."""
import argparse
import ast
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--source", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
if args.output.exists():
    parser.error("Refusing to overwrite an existing translation lookup.")

messages = {}
key = value = active = None
for line in args.source.read_text(encoding="utf-8").splitlines():
    line = line.strip()
    if line.startswith("msgid "):
        if key is not None and value is not None:
            messages[key] = value
        key = ast.literal_eval(line[6:])
        value = None
        active = "key"
    elif line.startswith("msgstr "):
        value = ast.literal_eval(line[7:])
        active = "value"
    elif line.startswith('"'):
        if active == "key":
            key += ast.literal_eval(line)
        elif active == "value":
            value += ast.literal_eval(line)
if key is not None and value is not None:
    messages[key] = value
args.output.write_text(json.dumps(messages, ensure_ascii=False), encoding="utf-8")
