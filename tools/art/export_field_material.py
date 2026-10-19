#!/usr/bin/env python3
"""Export the approved world field without changing its UV scale or source art."""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image
from export_overrides import ROOT, read_json, within
from export_environment_overrides import PACK

MANIFEST = PACK / 'world-materials-v1/manifest-field-material-v2.json'


def match_period_edges(image):
    """Match opposite outermost pixels only; preserve every interior pixel."""
    width, height = image.size
    if image.mode != 'RGB' or min(width, height) < 3:
        raise ValueError('expected an RGB material at least 3px per axis')
    result = image.copy()
    average = lambda values: tuple(sum(pixel[channel] for pixel in values) // len(values) for channel in range(3))
    for x in range(1, width - 1):
        pixel = average([image.getpixel((x, 0)), image.getpixel((x, height - 1))])
        result.putpixel((x, 0), pixel)
        result.putpixel((x, height - 1), pixel)
    for y in range(1, height - 1):
        pixel = average([image.getpixel((0, y)), image.getpixel((width - 1, y))])
        result.putpixel((0, y), pixel)
        result.putpixel((width - 1, y), pixel)
    corners = [(0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)]
    corner = average([image.getpixel(point) for point in corners])
    for point in corners:
        result.putpixel(point, corner)
    return result


def pixel_hash(image):
    return hashlib.sha256(image.tobytes()).hexdigest()


def plan(root, manifest):
    data = read_json(manifest)
    required = {'schemaVersion', 'id', 'replaces', 'source', 'sourceSha256', 'logicalSize', 'rasterSize', 'worldOrigin', 'fit', 'png', 'png2x'}
    if set(data) != required or data['schemaVersion'] != 2:
        raise ValueError('expected field material schemaVersion 2')
    size = data['logicalSize']
    if not isinstance(size, list) or len(size) != 2 or any(type(n) is not int or n < 3 for n in size):
        raise ValueError('expected positive integer logicalSize at least 3')
    raster_size = data['rasterSize']
    if not isinstance(raster_size, list) or len(raster_size) != 2 or any(type(n) is not int or n < 3 for n in raster_size):
        raise ValueError('expected positive integer rasterSize at least 3')
    if raster_size[0] * size[1] != raster_size[1] * size[0]:
        raise ValueError('raster and world aspect must agree')
    if data['worldOrigin'] != [0, 0] or data['fit'] != {'inset': 0, 'bleed': 0}:
        raise ValueError('world field requires zero origin and uncropped fit')
    binding = data['replaces']
    if not isinstance(binding, dict) or set(binding) != {'atlas', 'tone'}:
        raise ValueError('expected an explicit atlas/tone replacement')
    pack = root / PACK
    environment = read_json(pack / 'environment-builder-v1/manifest-environment-builder-v1.json')
    matches = [atlas for atlas in environment['atlases'] if atlas['id'] == binding['atlas']]
    if len(matches) != 1 or matches[0]['category'] != 'base-surface' or type(binding['tone']) is not int or not 0 <= binding['tone'] < matches[0]['variants']:
        raise ValueError('replacement does not identify one existing surface material')
    source = within(pack, data['source'])
    if not source.is_relative_to((pack / 'masters').resolve()):
        raise ValueError('field source must live under pack masters')
    raw = source.read_bytes()
    if hashlib.sha256(raw).hexdigest() != data['sourceSha256']:
        raise ValueError('field source SHA-256 mismatch')
    with Image.open(io.BytesIO(raw)) as opened:
        if opened.format != 'PNG' or opened.mode not in ('RGB', 'RGBA'):
            raise ValueError('expected opaque RGB or RGBA PNG')
        if opened.mode == 'RGBA' and opened.getchannel('A').getextrema() != (255, 255):
            raise ValueError('field source must be fully opaque')
        if opened.width * size[1] != opened.height * size[0] or opened.width < raster_size[0] * 2:
            raise ValueError('source must match logical aspect and support the declared raster size')
        image = opened.convert('RGB')
    planned = []
    for density, key in ((1, 'png'), (2, 'png2x')):
        target = within(manifest.parent, data[key])
        if target.parent != (manifest.parent / 'runtime').resolve() or target.suffix != '.png':
            raise ValueError('field output must be a PNG in its runtime directory')
        dimensions = tuple(n * density for n in raster_size)
        resized = image.resize(dimensions, Image.Resampling.LANCZOS)
        exported = match_period_edges(resized)
        width, height = dimensions
        report = {
            'id': data['id'], 'source': str(source.relative_to(root)),
            'sourceSha256': data['sourceSha256'], 'sourceSize': list(image.size),
            'output': str(target.relative_to(root)), 'exportDensity': density, 'size': list(dimensions),
            'logicalSize': size, 'rasterSize': raster_size,
            'pixelsPerWorldUnit': [dimensions[axis] / size[axis] for axis in range(2)], 'worldOrigin': data['worldOrigin'], 'fit': data['fit'],
            'replaces': binding, 'edgeBandPixels': 1,
            'derivation': 'full-canvas-lanczos-resize + opposite-outermost-pixel-average; no crop, blur, recolor or painting',
            'pixelSha256': pixel_hash(exported),
            'interiorPixelSha256': pixel_hash(exported.crop((1, 1, width - 1, height - 1))),
            'edgeHashes': {name: pixel_hash(exported.crop(box)) for name, box in {
                'top': (0, 0, width, 1), 'bottom': (0, height - 1, width, height),
                'left': (0, 0, 1, height), 'right': (width - 1, 0, width, height)}.items()},
            'visualRepeatReview': 'required; matching border pixels does not prove large-plane continuity',
        }
        planned.append((target, exported, report))
    if planned[0][0] == planned[1][0]:
        raise ValueError('density outputs must have distinct paths')
    return planned


def export(root, manifest, check=False):
    planned = plan(root, manifest)
    for target, image, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != image.mode or current.size != image.size or current.tobytes() != image.tobytes():
                    raise ValueError(f'stale field material export: {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target, optimize=True)
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / MANIFEST, args.check), indent=2))
