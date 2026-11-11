#!/usr/bin/env python3
"""Compare complete official saves without changing either input file."""

import argparse
import copy
import hashlib
import json
from pathlib import Path


def read(path):
    data = path.read_bytes()
    return json.loads(data), hashlib.sha256(data).hexdigest()


def comparison_copy(document):
    result = copy.deepcopy(document)
    timestamps = {}
    for owner in ("campaign", "battle"):
        timestamp = result[owner].pop("savedAt")
        if not isinstance(timestamp, str) or not timestamp:
            raise ValueError(f"Missing or invalid {owner}.savedAt")
        timestamps[f"{owner}.savedAt"] = timestamp
    return result, timestamps


def differences(left, right, path="$", found=None):
    found = [] if found is None else found
    if len(found) >= 20:
        return found
    if isinstance(left, dict) and isinstance(right, dict):
        if left.keys() != right.keys():
            found.append(path + ": object keys differ")
        for key in sorted(left.keys() & right.keys()):
            differences(left[key], right[key], path + "." + key, found)
    elif isinstance(left, list) and isinstance(right, list):
        if len(left) != len(right):
            found.append(path + ": array length differs")
        for index, (a, b) in enumerate(zip(left, right)):
            differences(a, b, f"{path}[{index}]", found)
    elif isinstance(left, bool) != isinstance(right, bool) or left != right:
        found.append(path + ": value or type differs")
    return found


def compare(expected, actual):
    expected_copy, expected_times = comparison_copy(expected)
    actual_copy, actual_times = comparison_copy(actual)
    return differences(expected_copy, actual_copy), {
        key: {"expected": value, "actual": actual_times[key], "changed": value != actual_times[key]}
        for key, value in expected_times.items()
    }


def self_test(source):
    changed = copy.deepcopy(source)
    for owner in ("campaign", "battle"):
        changed[owner]["savedAt"] = "self-test-only-detached-copy"
    if compare(source, changed)[0]:
        raise AssertionError("Timestamp-only changes were rejected")
    mutations = {
        "hp": lambda value: value["battle"]["state"]["units"][0].update(hp=-1),
        "facing": lambda value: value["battle"]["state"]["units"][0].update(facing="invalid-self-test"),
        "position": lambda value: value["battle"]["state"]["units"][0].update(x=-999),
        "events": lambda value: value["events"].append({"type": "invalid-self-test"}),
        "campaign": lambda value: value["campaign"]["state"].update(selfTest=True),
        "top-level": lambda value: value.update(selfTest=True),
    }
    for name, mutate in mutations.items():
        candidate = copy.deepcopy(changed)
        mutate(candidate)
        if not compare(source, candidate)[0]:
            raise AssertionError(f"Mutation accepted: {name}")
    if not differences(True, 1):
        raise AssertionError("Boolean/number substitution accepted")
    return {"passed": True, "timestampOnlyAccepted": True, "mutationsRejected": list(mutations), "booleanNumberRejected": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("expected", type=Path)
    parser.add_argument("actual", nargs="?", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    arguments = parser.parse_args()
    expected, expected_hash = read(arguments.expected)
    if arguments.self_test:
        report = self_test(expected)
    else:
        if arguments.actual is None:
            parser.error("actual save is required unless --self-test is selected")
        actual, actual_hash = read(arguments.actual)
        changed, times = compare(expected, actual)
        # Requiring a fresh official save prevents a copied input from being
        # mistaken for an F5 write from the actual observed application state.
        fresh = all(value["changed"] for value in times.values())
        report = {
            "passed": not changed and fresh,
            "completeSaveEqualExceptTwoWriterTimestamps": not changed,
            "freshOfficialSaveTimestamps": fresh,
            "omittedPaths": ["$.campaign.savedAt", "$.battle.savedAt"],
            "expectedFile": str(arguments.expected.resolve()),
            "expectedSha256": expected_hash,
            "actualFile": str(arguments.actual.resolve()),
            "actualSha256": actual_hash,
            "timestamps": times,
            "differences": changed,
        }
    encoded = json.dumps(report, indent=2) + "\n"
    if arguments.output:
        with arguments.output.open("x") as destination:
            destination.write(encoded)
    print(encoded, end="")
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
