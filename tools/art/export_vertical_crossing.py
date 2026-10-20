#!/usr/bin/env python3
"""Export the reviewed, full-canvas vertical stone source at independent densities."""
import argparse
import json
from export_crossing import ROOT, PACK, plan_registered_crossing

MANIFEST = PACK / 'registered-crossings-v1/manifest-crossing-vertical-v1.json'


def plan(root, manifest):
    return plan_registered_crossing(root, manifest, 'vertical')


def export(root, manifest, check=False):
    planned = plan(root, manifest)
    for target, raw, _, _ in planned:
        if check:
            if target.read_bytes() != raw:
                raise ValueError('stale vertical crossing export')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
    return [report for _, _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / MANIFEST, args.check), indent=2))
