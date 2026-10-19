#!/usr/bin/env python3
"""Export reviewed opaque four-source surface tiles with explicitly approved outer-pixel seam matching."""
import argparse
import hashlib
import io
import json
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, read_json, within
from export_environment_overrides import PACK

CATALOG = Path('tools/art/surface-overrides.json')
ORDER = [f'meadow-{i}' for i in range(1, 5)]
SURFACE_ORDERS = {'surface-meadow': ORDER, 'surface-old-stone': [f'old-stone-{i}' for i in range(1, 5)],
                  'surface-graveyard': [f'graveyard-{i}' for i in range(1, 5)],
                  'surface-forge-stone': [f'forge-stone-{i}' for i in range(1, 5)],
                  'surface-forest-floor': [f'forest-floor-{i}' for i in range(1, 5)],
                  'surface-earth': [f'earth-{i}' for i in range(1, 5)],
                  'surface-wasteland': [f'wasteland-{i}' for i in range(1, 5)],
                  'surface-snow': [f'snow-{i}' for i in range(1, 5)]}


def pixel_hash(image):
    return hashlib.sha256(image.tobytes()).hexdigest()


def match_edges(cells):
    """Change only the outermost row/column, sharing opposite family edges."""
    size = cells[0].width
    if len(cells) != 4 or any(c.mode != 'RGB' or c.size != (size, size) for c in cells) or size not in (32, 64):
        raise ValueError('expected four RGB tiles at32 or64')
    outputs = [cell.copy() for cell in cells]
    horizontal = [tuple(sum(c.getpixel((x, y))[k] for c in cells for y in (0, size - 1)) // 8 for k in range(3)) for x in range(size)]
    vertical = [tuple(sum(c.getpixel((x, y))[k] for c in cells for x in (0, size - 1)) // 8 for k in range(3)) for y in range(size)]
    corner = tuple(sum(edge[k] for edge in (horizontal[0], horizontal[-1], vertical[0], vertical[-1])) // 4 for k in range(3))
    horizontal[0] = horizontal[-1] = vertical[0] = vertical[-1] = corner
    for out in outputs:
        for x in range(size):
            out.putpixel((x, 0), horizontal[x])
            out.putpixel((x, size - 1), horizontal[x])
        for y in range(size):
            out.putpixel((0, y), vertical[y])
            out.putpixel((size - 1, y), vertical[y])
    return outputs


def seam_report(before, after):
    size = after.width
    edges = {'top': (0, 0, size, 1), 'bottom': (0, size - 1, size, size),
             'left': (0, 0, 1, size), 'right': (size - 1, 0, size, size)}
    changed = 0
    maximum = 0
    for y in range(size):
        for x in range(size):
            a, b = before.getpixel((x, y)), after.getpixel((x, y))
            if a != b:
                if x not in (0, size - 1) and y not in (0, size - 1):
                    raise ValueError('seam matching changed interior')
                changed += 1
                maximum = max(maximum, max(abs(a[k] - b[k]) for k in range(3)))
    return {'edgeBandPixels': 1, 'changedPixels': changed, 'maxChannelChange': maximum,
            'interiorPixelSha256': pixel_hash(after.crop((1, 1, size - 1, size - 1))),
            'edgeHashes': {name: pixel_hash(after.crop(box)) for name, box in edges.items()},
            'pixelSha256': pixel_hash(after)}


def plan(root, catalog):
    data = read_json(catalog)
    if set(data) != {'schemaVersion', 'atlases'} or data['schemaVersion'] != 1 or not isinstance(data['atlases'], dict):
        raise ValueError('expected schemaVersion1 and atlases')
    pack = root / PACK
    env = pack / 'environment-builder-v1'
    manifest = read_json(env / 'manifest-environment-builder-v1.json')
    planned = []
    for name, override in data['atlases'].items():
        if name not in SURFACE_ORDERS:
            raise ValueError('unsupported surface atlas')
        order = SURFACE_ORDERS[name]
        matches = [a for a in manifest['atlases'] if a['id'] == name]
        if len(matches) != 1:
            raise ValueError('expected one surface manifest record')
        a = matches[0]
        if (a['columns'], a['rows'], a['cellWidth'], a['cellHeight'], a['cellWidth2x'], a['cellHeight2x'], a['componentCount'], a['category'], a['tileMode'], a['variants'], a['edgeLocked'], a['cellOrder']) != (4, 1, 32, 32, 64, 64, 4, 'base-surface', 'variants', 4, True, order):
            raise ValueError('unexpected surface contract')
        if not isinstance(override, dict) or set(override) != {'cells'} or set(override['cells']) != set(order):
            raise ValueError('exact four surface sources required')
        sources, provenance = [], []
        for cell_id in order:
            entry = override['cells'][cell_id]
            if not isinstance(entry, dict) or set(entry) != {'source', 'sha256'}:
                raise ValueError('expected source and sha256')
            path = within(root, entry['source'])
            if not path.is_relative_to((pack / 'masters').resolve()):
                raise ValueError('surface master must be in own pack masters')
            raw = path.read_bytes()
            if hashlib.sha256(raw).hexdigest() != entry['sha256']:
                raise ValueError('source SHA-256 mismatch')
            with Image.open(io.BytesIO(raw)) as opened:
                if opened.format != 'PNG' or opened.mode not in ('RGB', 'RGBA') or opened.width != opened.height or opened.width < 64:
                    raise ValueError('expected square opaque PNG source at least64px')
                if opened.mode == 'RGBA' and opened.getchannel('A').getextrema() != (255, 255):
                    raise ValueError('base surface must be fully opaque')
                source = opened.convert('RGB')
            sources.append(source)
            provenance.append({'cellId': cell_id, 'source': entry['source'], 'sourceSha256': entry['sha256'], 'sourceSize': list(source.size)})
        raw_hd = [s.resize((64, 64), Image.Resampling.LANCZOS) for s in sources]
        hd = match_edges(raw_hd)
        raw_small = [s.resize((32, 32), Image.Resampling.LANCZOS) for s in sources]
        small = match_edges(raw_small)
        for density, size, before, cells, key in ((1, 32, raw_small, small, 'png'), (2, 64, raw_hd, hd, 'png2x')):
            target = within(env, a[key])
            if target != (env / 'runtime/atlas' / f'{name}{"@2x" if density == 2 else ""}.png').resolve():
                raise ValueError('unexpected surface destination')
            sheet = Image.new('RGB', (size * 4, size))
            reports = []
            for i, (original, cell) in enumerate(zip(before, cells)):
                sheet.paste(cell, (i * size, 0))
                reports.append({'cellId': order[i], **seam_report(original, cell)})
            for report in reports:
                edges = report['edgeHashes']
                if edges['top'] != reports[0]['edgeHashes']['bottom'] or edges['left'] != reports[0]['edgeHashes']['right']:
                    raise ValueError('surface edges do not match')
            planned.append((target, sheet, {'atlasId': name, 'density': density, 'output': str(target.relative_to(root)),
                'size': list(sheet.size), 'sources': provenance, 'cells': reports,
                'derivation': 'full-canvas-lanczos-resize + outermost-pixel-family-edge-average; no blur/recolor',
                'pixelSha256': pixel_hash(sheet), 'visualSeamReview': 'required-separately'}))
    return planned


def seam_previews(sheet):
    """Repeat actual exported pixels; these previews do not paint or blend art."""
    size = sheet.height
    if sheet.size != (size * 4, size) or size not in (32, 64):
        raise ValueError('unexpected surface sheet')
    cells = [sheet.crop((i * size, 0, (i + 1) * size, size)) for i in range(4)]
    repeated = Image.new('RGB', (size * 8, size * 2))
    mixed = Image.new('RGB', (size * 4, size * 4))
    for i, cell in enumerate(cells):
        for y in range(2):
            for x in range(2):
                repeated.paste(cell, ((i * 2 + x) * size, y * size))
    for y in range(4):
        for x in range(4):
            mixed.paste(cells[(x + y) % 4], (x * size, y * size))
    return {'repeated-2x2': repeated, 'mixed-4x4': mixed}


def export(root, catalog, check=False):
    planned = plan(root, catalog)
    for target, sheet, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != sheet.mode or current.size != sheet.size or current.tobytes() != sheet.tobytes():
                    raise ValueError('stale surface export')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            sheet.save(target, optimize=True)
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--preview-dir', type=Path)
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))

    if args.preview_dir:
        args.preview_dir.mkdir(parents=True, exist_ok=True)
        for _, sheet, report in plan(ROOT, ROOT / CATALOG):
            for name, preview in seam_previews(sheet).items():
                preview.save(args.preview_dir / f'{report["atlasId"]}-{report["density"]}x-{name}.png')
