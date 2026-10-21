#!/usr/bin/env python3
"""Source guard for the shipped singular, context-free PO catalog; Godot owns runtime parsing."""
import argparse
import json
import re
from pathlib import Path


class CatalogError(ValueError):
    pass


def read_catalog(source: str) -> dict[str, str]:
    messages: dict[str, str] = {}
    entry: dict[str, str] = {}
    flags: set[str] = set()
    active = None
    header = None

    def finish():
        nonlocal entry, flags, active, header
        if not entry:
            return
        if set(entry) != {"msgid", "msgstr"}:
            raise CatalogError("Every entry requires one msgid and one msgstr.")
        if "fuzzy" in flags:
            raise CatalogError("Fuzzy entries are not publishable.")
        key, value = entry["msgid"], entry["msgstr"]
        if not key:
            if header is not None:
                raise CatalogError("Duplicate PO header.")
            header = value
        else:
            if not re.fullmatch(r"[a-z][a-z0-9_-]*(?:\.[a-z0-9_-]+)+", key):
                raise CatalogError(f"Invalid stable message key: {key!r}.")
            if key in messages:
                raise CatalogError(f"Duplicate message key: {key}.")
            if not value.strip():
                raise CatalogError(f"Empty translation: {key}.")
            messages[key] = value
        entry, flags, active = {}, set(), None

    for number, raw in enumerate(source.splitlines(), 1):
        line = raw.strip()
        if not line:
            finish()
            continue
        if line.startswith("#~"):
            raise CatalogError(f"Obsolete entry at line {number}; remove it.")
        if line.startswith("#,"):
            flags.update(flag.strip() for flag in line[2:].split(","))
            continue
        if line.startswith("#"):
            continue
        match = re.fullmatch(r'(msgid|msgstr)\s+(".*")', line)
        if match:
            field, quoted = match.groups()
            if field == "msgid" and "msgstr" in entry:
                finish()
            if field in entry:
                raise CatalogError(f"Duplicate {field} at line {number}.")
            active = field
            entry[field] = ""
        elif line.startswith('"') and active is not None:
            quoted = line
        else:
            raise CatalogError(f"Malformed or unsupported PO directive at line {number}.")
        try:
            value = json.loads(quoted)
        except json.JSONDecodeError as error:
            raise CatalogError(f"Invalid quoted PO string at line {number}.") from error
        if not isinstance(value, str):
            raise CatalogError(f"Expected quoted PO string at line {number}.")
        entry[active] += value
    finish()
    if header is None or "Language: zh_CN\n" not in header:
        raise CatalogError("Catalog requires a zh_CN language header.")
    if "Content-Type: text/plain; charset=UTF-8\n" not in header:
        raise CatalogError("Catalog requires the UTF-8 content-type header.")
    if not messages:
        raise CatalogError("Catalog has no messages.")
    return messages


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path)
    parser.add_argument("--output", type=Path, help="Write a disposable check projection, never a runtime catalog.")
    arguments = parser.parse_args()
    messages = read_catalog(arguments.catalog.read_text(encoding="utf-8"))
    if arguments.output:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(json.dumps(messages, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"PASS PO source guard: {len(messages)} unique nonempty zh_CN messages; no fuzzy or obsolete entries.")


if __name__ == "__main__":
    main()
