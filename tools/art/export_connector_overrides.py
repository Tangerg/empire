#!/usr/bin/env python3
"""Bind the approved dirt-road/blank-edge pair to its existing runtime atlases."""
import argparse
import hashlib
import io
import json
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, read_json, within
from export_environment_overrides import PACK
from export_surface_overrides import match_edges
from connector_geometry import builder, geometry, compose

CATALOG = Path('tools/art/connector-overrides.json')


def downsample_cells(sheet):
    if sheet.mode != 'RGBA' or sheet.size != (1024, 256):
        raise ValueError('expected the complete native2x 64-cell connector sheet')
    output = Image.new('RGBA', (512, 128))
    for index in range(64):
        x, y = index % 16 * 64, index // 16 * 64
        cell = sheet.crop((x, y, x + 64, y + 64)).resize((32, 32), Image.Resampling.LANCZOS)
        output.paste(cell, (index % 16 * 32, index // 16 * 32))
    return output


def match_open_port_rgb(sheet):
    """Average existing opposite open-port RGB on the outermost pixels only."""
    if sheet.mode != 'RGBA' or sheet.size != (512, 128):
        raise ValueError('expected an independently downsampled 1x connector sheet')
    output = sheet.copy()
    writes = {}
    for sides in (((1, 'north'), (4, 'south')), ((8, 'west'), (2, 'east'))):
        for position in range(32):
            points = []
            for bit, side in sides:
                for mask in range(16):
                    if not mask & bit:
                        continue
                    for variant in range(4):
                        index = mask * 4 + variant
                        x, y = index % 16 * 32, index // 16 * 32
                        dx, dy = {'north': (position, 0), 'south': (position, 31),
                                  'west': (0, position), 'east': (31, position)}[side]
                        points.append((x + dx, y + dy))
            pixels = [sheet.getpixel(point) for point in points]
            if len({pixel[3] for pixel in pixels}) != 1:
                raise ValueError('opposing open-port alpha differs; RGB matching cannot repair geometry')
            rgb = tuple(sum(pixel[channel] for pixel in pixels) // len(pixels) for channel in range(3))
            for point in points:
                if point in writes and writes[point] != rgb:
                    raise ValueError('open-port RGB corrections disagree at a corner')
                writes[point] = rgb
    changed = 0
    maximum = 0
    premultiplied = 0
    opaque = 0
    for point, rgb in writes.items():
        previous = sheet.getpixel(point)
        changed += previous[:3] != rgb
        maximum = max(maximum, *(abs(previous[channel] - rgb[channel]) for channel in range(3)))
        premultiplied = max(premultiplied, *(abs(previous[channel] * previous[3] // 255 - rgb[channel] * previous[3] // 255) for channel in range(3)))
        if previous[3] == 255:
            opaque = max(opaque, *(abs(previous[channel] - rgb[channel]) for channel in range(3)))
        output.putpixel(point, (*rgb, previous[3]))
    report = {'operation': 'existing opposite open-port RGB average on outermost pixels only',
              'changedPixels': changed, 'maximumRGBDelta': maximum, 'maximumPremultipliedRGBDelta': premultiplied,
              'maximumOpaqueRGBDelta': opaque, 'alphaChanged': False,
              'beforePixelSha256': hashlib.sha256(sheet.tobytes()).hexdigest(),
              'afterPixelSha256': hashlib.sha256(output.tobytes()).hexdigest()}
    return output, report


def plan(root, catalog):
    data = read_json(catalog)
    required = {'schemaVersion', 'atlas', 'edgeAtlas', 'source', 'sha256', 'geometrySource', 'geometrySha256', 'roadWidthLogical', 'approved2xOutputs', 'previous1xOutputs', 'derivation1x'}
    if set(data) != required or data['schemaVersion'] != 1 or data['atlas'] != 'route-dirt-road' or data['edgeAtlas'] != 'route-edge-dirt-road' or data['roadWidthLogical'] != 22:
        raise ValueError('expected the reviewed 22-unit dirt-road and blank-edge pair')
    if data['derivation1x'] != 'independent-64-to-32-lanczos-then-open-port-rgb-match':
        raise ValueError('1x connectors must be downsampled independently before packing')
    pack = root / PACK
    env = pack / 'environment-builder-v1'
    code = within(root, data['geometrySource'])
    if code != (env / 'tools/build_environment_builder_v1.py').resolve() or hashlib.sha256(code.read_bytes()).hexdigest() != data['geometrySha256']:
        raise ValueError('connector geometry source SHA-256 mismatch')
    source = within(root, data['source'])
    if not source.is_relative_to((pack / 'masters').resolve()) or hashlib.sha256(source.read_bytes()).hexdigest() != data['sha256']:
        raise ValueError('connector material source SHA-256 mismatch')
    with Image.open(source) as image:
        if image.format != 'PNG' or image.width != image.height or image.width < 64 or image.convert('RGBA').getchannel('A').getextrema() != (255, 255):
            raise ValueError('connector material must be a square opaque PNG')
        resized = image.convert('RGB').resize((64, 64), Image.Resampling.LANCZOS)
    texture = match_edges([resized] * 4)[0]
    mod, _ = builder(root)
    sheet = Image.new('RGBA', (1024, 256))
    for mask in range(16):
        shape = geometry(mod, mask, road_width_logical=data['roadWidthLogical'])
        cell = compose(texture, shape, (72, 61, 45), thin=True)
        for variant in range(4):
            index = mask * 4 + variant
            sheet.paste(cell, (index % 16 * 64, index // 16 * 64))
    manifest = read_json(env / 'manifest-environment-builder-v1.json')
    planned = []
    for name, output in ((data['atlas'], sheet), (data['edgeAtlas'], Image.new('RGBA', (1024, 256)))):
        matches = [a for a in manifest['atlases'] if a['id'] == name]
        if len(matches) != 1:
            raise ValueError('expected one runtime connector atlas')
        atlas = matches[0]
        if (atlas['columns'], atlas['rows'], atlas['cellWidth'], atlas['cellHeight'], atlas['cellWidth2x'], atlas['cellHeight2x'], atlas['componentCount'], atlas['tileMode'], atlas['maskOrder'], atlas['bitOrder'], atlas['variantsPerMask'], atlas['cellIndex']) != (16, 4, 32, 32, 64, 64, 64, 'nesw-16-variants', list(range(16)), ['N', 'E', 'S', 'W'], 4, 'mask * 4 + variant'):
            raise ValueError('unexpected runtime connector topology')
        for density, key in ((1, 'png'), (2, 'png2x')):
            image, seam = (output, None) if density == 2 else match_open_port_rgb(downsample_cells(output))
            target = within(env, atlas[key])
            expected = env / 'runtime/atlas' / f'{name}{"@2x" if density == 2 else ""}.png'
            if target != expected.resolve():
                raise ValueError('unexpected runtime connector output path')
            buffer = io.BytesIO()
            image.save(buffer, format='PNG')
            digest = hashlib.sha256(buffer.getvalue()).hexdigest()
            if density == 2 and data['approved2xOutputs'].get(target.name) != digest:
                raise ValueError('connector pixels differ from the reviewed export')
            planned.append((target, buffer.getvalue(), image, {
                'atlas': name, 'density': density, 'size': list(image.size), 'source': data['source'],
                'sourceSha256': data['sha256'], 'geometrySource': data['geometrySource'], 'geometrySha256': data['geometrySha256'],
                'roadWidthLogical': data['roadWidthLogical'], 'output': str(target.relative_to(root)),
                'sha256': digest, 'approved2xExportExact': density == 2,
                'derivation1x': data['derivation1x'] if density == 1 else None, 'edgeMatching': seam,
                'previousWholeSheet1xSha256': data['previous1xOutputs'].get(target.name), 'topology': 'NESW16 × four identical material variants',
                'shoulder': 'approved one-HD-pixel alpha ring; blank companion avoids double borders',
                'material': 'full-source resize plus approved one-pixel edge average; no recolor or painted changes',
            }))
    if set(data['approved2xOutputs']) | set(data['previous1xOutputs']) != {target.name for target, _, _, _ in planned}:
        raise ValueError('approved outputs must identify exactly the road/edge pair at both densities')
    return planned


def export(root, catalog, check=False):
    planned = plan(root, catalog)
    for target, raw, image, _ in planned:
        if check:
            if target.read_bytes() != raw:
                raise ValueError(f'stale connector export: {target.name}')
        else:
            target.write_bytes(raw)
    return [report for _, _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))
