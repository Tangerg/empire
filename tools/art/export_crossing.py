#!/usr/bin/env python3
"""Export the approved full-canvas horizontal crossing at its registered scale."""
import argparse
import hashlib
import io
import json
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, within, read_json, validate_alpha
from export_environment_overrides import PACK, finite_number, point

MANIFEST = PACK / 'registered-crossings-v1/manifest-crossing-v1.json'


def plan_registered_crossing(root, manifest, orientation):
    data = read_json(manifest)
    required = {'schemaVersion', 'id', 'orientation', 'source', 'sourceSha256', 'sourceSize', 'sourceFoot', 'logicalPerSourcePixel', 'png', 'png2x', 'approvedExports'}
    if set(data) != required or data['schemaVersion'] != 1 or data['orientation'] != orientation:
        raise ValueError(f'expected the authored {orientation} crossing manifest')
    source = within(root / PACK, data['source'])
    if not source.is_relative_to((root / PACK / 'masters').resolve()) or hashlib.sha256(source.read_bytes()).hexdigest() != data['sourceSha256']:
        raise ValueError('crossing source SHA-256 mismatch')
    scale, foot = data['logicalPerSourcePixel'], data['sourceFoot']
    with Image.open(source) as opened:
        if opened.format != 'PNG' or opened.mode != 'RGBA' or list(opened.size) != data['sourceSize']:
            raise ValueError('crossing requires the complete declared RGBA source canvas')
        image = opened.copy()
    if not finite_number(scale) or scale <= 0 or not point(foot) or any(foot[axis] < 0 or foot[axis] > image.size[axis] for axis in (0, 1)):
        raise ValueError('invalid crossing scale or source contact')
    validate_alpha(image, data['id'])
    logical = [value * scale for value in image.size]
    anchor = [value * scale for value in foot]
    planned = []
    for density, key in ((1, 'png'), (2, 'png2x')):
        size = tuple(round(value * density) for value in logical)
        if any(size[axis] <= 0 or size[axis] > image.size[axis] for axis in (0, 1)):
            raise ValueError('crossing density must use native source detail without upscaling')
        output = image.resize(size, Image.Resampling.LANCZOS)
        alpha = validate_alpha(output, f'{data["id"]}@{density}x')
        target = within(manifest.parent, data[key])
        if target.parent != (manifest.parent / 'runtime').resolve() or target.suffix != '.png':
            raise ValueError('crossing export must remain in its runtime directory')
        buffer = io.BytesIO()
        output.save(buffer, format='PNG')
        digest = hashlib.sha256(buffer.getvalue()).hexdigest()
        if data['approvedExports'].get(key) != digest:
            raise ValueError('crossing export differs from the approved registered image')
        planned.append((target, buffer.getvalue(), output, {
            'id': data['id'], 'source': str(source.relative_to(root)), 'sourceSha256': data['sourceSha256'],
            'sourceSize': list(image.size), 'sourceFoot': foot, 'logicalPerSourcePixel': scale,
            'logicalSize': logical, 'logicalAnchor': anchor, 'output': str(target.relative_to(root)),
            'density': density, 'size': list(size), 'actualPixelsPerWorldUnit': [size[i] / logical[i] for i in (0, 1)],
            'alpha': alpha, 'sha256': digest, 'approvedExportExact': True,
            'derivation': 'full-canvas Lanczos resize only; independently rounded native densities; no crop, recolor, edge changes or alpha painting',
        }))
    if len({target for target, _, _, _ in planned}) != 2:
        raise ValueError('crossing outputs must be distinct')
    return planned


def plan(root, manifest):
    return plan_registered_crossing(root, manifest, 'horizontal')


def export(root, manifest, check=False):
    planned = plan(root, manifest)
    for target, raw, _, _ in planned:
        if check:
            if target.read_bytes() != raw:
                raise ValueError('stale crossing export')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
    return [report for _, _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / MANIFEST, args.check), indent=2))
