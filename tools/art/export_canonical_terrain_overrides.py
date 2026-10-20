#!/usr/bin/env python3
"""Export explicitly registered reviewed Border, Capital, Forge, Graveyard and Oathlight materials without changing canonical topology."""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image, ImageDraw

from export_overrides import ROOT, read_json, within
from export_environment_overrides import PACK
from export_surface_overrides import match_edges, pixel_hash, seam_report, seam_previews

CATALOG = Path('tools/art/canonical-terrain-overrides.json')
TOPICS = {f'C01-TERRAIN-BORDER-{i}': 'variants' if i in (1, 3) else 'nesw-16' for i in range(1, 5)}
TOPICS.update({f'C01-TERRAIN-CAPITAL-{i}': 'variants' if i in (3, 4) else 'nesw-16' for i in range(1, 5)})
TOPICS.update({f'C01-TERRAIN-FORGE-{i}': 'nesw-16' if i == 2 else 'variants' for i in range(1, 5)})
TOPICS.update({f'C01-TERRAIN-GRAVEYARD-{i}': 'variants' for i in range(1, 5)})
TOPICS.update({f'C01-TERRAIN-OATHLIGHT-{i}': 'variants' if i in (1, 4) else 'nesw-16' for i in range(1, 5)})
TOPICS.update({f'C01-TERRAIN-SILVERWOOD-{i}': 'nesw-16' if i == 2 else 'variants' for i in range(1, 5)})
VARIANTS = ['a', 'b', 'c', 'd']
# The original 1x and 2x builders rasterized independently. These are hashes of
# the complete historical horizontal alpha strips, not resized 1x masks.
ALPHA_SHA256 = {
    ('variants', 32): 'f47a8ec3e9aff2318d896942282ad4fe37d6391c82914f54a5da8a37de1300c6',
    ('variants', 64): '0fbba07a833d4dcfc7024eaf313661a0ba8f80a05c6d29b8801c612e10e60dee',
    ('nesw-16', 32): 'c9ca0ff6335c19893b720ceed9b74cea27b0a9b225696f1efa0b9216de85ef3a',
    ('nesw-16', 64): 'e68987b3584c2836052c49d69606a404d891538bc20448f4e4206255e8fecb3e',
}


def canonical_masks(size):
    if size not in (32, 64):
        raise ValueError('canonical tile size must be 32 or 64')
    core, radius, arms = (
        ((10, 10, 21, 21), 4, ((12, 0, 19, 16), (16, 12, 31, 19), (12, 16, 19, 31), (0, 12, 16, 19)))
        if size == 32 else
        ((20, 20, 43, 43), 8, ((24, 0, 39, 32), (32, 24, 63, 39), (24, 32, 39, 63), (0, 24, 32, 39)))
    )
    masks = []
    strip = Image.new('L', (size * 16, size))
    for bits in range(16):
        mask = Image.new('L', (size, size))
        draw = ImageDraw.Draw(mask)
        draw.rounded_rectangle(core, radius=radius, fill=255)
        for bit, box in zip((1, 2, 4, 8), arms):
            if bits & bit:
                draw.rectangle(box, fill=255)
        masks.append(mask)
        strip.paste(mask, (bits * size, 0))
    if pixel_hash(strip) != ALPHA_SHA256['nesw-16', size]:
        raise ValueError('historical canonical alpha rasterization changed')
    return masks


def validate_manifests(root, topics):
    pack = root / PACK
    base = read_json(pack / 'manifest-final-fantasy-v1.json')
    hd = read_json(pack / 'manifest-tactical-runtime-hd.json')
    if (hd.get('schemaVersion') != 'candidate-01-runtime-hd-v2' or 'pixelDensity' in hd
            or type(hd.get('assetCount')) is not int or hd['assetCount'] != len(hd['assets'])):
        raise ValueError('unexpected canonical HD manifest contract')
    targets = {}
    for topic in topics:
        mode = TOPICS[topic]
        count = 4 if mode == 'variants' else 16
        slug = topic.removeprefix('C01-').lower()
        matches = [a for a in base['assets'] if a.get('topicId') == topic]
        high = [a for a in hd['assets'] if a.get('topicId') == topic]
        if len(matches) != 1 or len(high) != 1:
            raise ValueError('expected exactly one canonical and HD record per topic')
        asset, high = matches[0], high[0]
        expected = {'assetId': topic.replace('C01-', 'C01-FFV1-', 1), 'category': 'terrain',
                    'png': f'runtime/terrain/{slug}.png', 'width': count * 32, 'height': 32,
                    'tileWidth': 32, 'tileHeight': 32, 'tileMode': mode, 'runtimeReady': True}
        if mode == 'variants':
            expected.update(variants=4, variantOrder=VARIANTS)
            forbidden = ('connectionMasks', 'maskBits')
        else:
            expected.update(connectionMasks=16, maskBits=['N', 'E', 'S', 'W'])
            forbidden = ('variants', 'variantOrder')
        if (any(asset.get(key) != value for key, value in expected.items())
                or any(key in asset for key in (*forbidden, 'anchor', 'footprint', 'pixelDensity'))):
            raise ValueError('unexpected canonical terrain geometry or metadata')
        if (high != {'topicId': topic, 'png': f'runtime-hd/terrain/{slug}.png',
                     'width': count * 64, 'height': 64, 'pixelDensity': 2}):
            raise ValueError('unexpected canonical HD terrain geometry or metadata')
        paths = []
        for record in (asset, high):
            target = within(root, str(PACK / record['png']))
            # Resolve the expected directory too; never follow a runtime symlink
            # into another pack directory or outside this repository.
            if target != root.resolve() / PACK / record['png']:
                raise ValueError('canonical terrain destination must not use symlinks')
            paths.append(target)
        targets[topic] = paths
    return targets


def read_material(root, entry, mode):
    if not isinstance(entry, dict) or set(entry) != {'source', 'sha256', 'registration'}:
        raise ValueError('material requires source, sha256 and registration only')
    digest = entry['sha256']
    if not isinstance(digest, str) or len(digest) != 64 or any(c not in '0123456789abcdef' for c in digest):
        raise ValueError('source SHA-256 must be 64 lowercase hex characters')
    path = within(root, entry['source'])
    if not path.is_relative_to((root / PACK / 'masters').resolve()):
        raise ValueError('canonical material must be in its own pack masters')
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != digest:
        raise ValueError('source SHA-256 mismatch')
    with Image.open(io.BytesIO(raw)) as opened:
        if (opened.format != 'PNG' or opened.mode not in ('RGB', 'RGBA')
                or getattr(opened, 'n_frames', 1) != 1 or min(opened.size) < 64):
            raise ValueError('expected single-frame RGB/RGBA PNG material at least 64px in each dimension')
        if opened.mode == 'RGBA' and opened.getchannel('A').getextrema() != (255, 255):
            raise ValueError('canonical material source must be fully opaque')
        source_mode = opened.mode
        source = opened.convert('RGB')
    registration = entry['registration']
    if not isinstance(registration, dict) or set(registration) != {'sourceSize', 'windowsXYXY'}:
        raise ValueError('registration requires sourceSize and windowsXYXY only')
    source_size = registration['sourceSize']
    if (not isinstance(source_size, list) or len(source_size) != 2
            or any(type(v) is not int for v in source_size) or source_size != list(source.size)):
        raise ValueError('registered sourceSize does not match actual canvas')
    windows = registration['windowsXYXY']
    count = 4 if mode == 'variants' else 1
    if not isinstance(windows, list) or len(windows) != count:
        raise ValueError(f'expected {count} explicit source windows')
    for box in windows:
        if (not isinstance(box, list) or len(box) != 4 or any(type(v) is not int for v in box)
                or not (0 <= box[0] < box[2] <= source.width and 0 <= box[1] < box[3] <= source.height)
                or box[2] - box[0] != box[3] - box[1] or box[2] - box[0] < 64):
            raise ValueError('source windows must be in-bounds integer squares at least 64px')
    side = windows[0][2] - windows[0][0]
    if any(box[2] - box[0] != side for box in windows):
        raise ValueError('all source windows must use one common material scale')
    if len({tuple(box) for box in windows}) != count:
        raise ValueError('opaque variants require distinct source windows')
    return source, {'source': entry['source'], 'sourceSha256': digest, 'sourceSize': list(source.size),
                    'sourceMode': source_mode, 'sourcePixelSha256': pixel_hash(source),
                    'registration': registration}


def edge_contract(cells, mode):
    size = cells[0].width
    pairs = {'eastWest': 0, 'southNorth': 0}
    for a, cell in enumerate(cells):
        for b, other in enumerate(cells):
            for name, bit, opposite, own_box, other_box in (
                ('eastWest', 2, 8, (size - 1, 0, size, size), (0, 0, 1, size)),
                ('southNorth', 4, 1, (0, size - 1, size, size), (0, 0, size, 1)),
            ):
                if mode == 'variants' or (a & bit and b & opposite):
                    if cell.crop(own_box).tobytes() != other.crop(other_box).tobytes():
                        raise ValueError(f'canonical terrain {name} edge mismatch: {a}, {b}')
                    pairs[name] += 1
    return {'comparison': 'exact RGBA equality for every allowed pair', 'pairs': pairs}


def render_material(source, windows, mode, size):
    raw = [source.crop(box).resize((size, size), Image.Resampling.LANCZOS) for box in windows]
    # The established family helper averages the opposite outermost edges.
    # Four references to the same connected material give its opposite-edge
    # average without inventing four authored sources or another seam algorithm.
    matched = match_edges(raw if mode == 'variants' else raw * 4)
    if mode == 'nesw-16':
        matched = matched[:1]
    material_reports = []
    for before, after, box in zip(raw, matched, windows):
        material_reports.append({'windowXYXY': box, 'sourcePixelsPerLogicalPixel': (box[2] - box[0]) / 32,
                                 'outputPixelsPerSourcePixel': size / (box[2] - box[0]),
                                 'samplePixelSha256': pixel_hash(before), **seam_report(before, after)})
    masks = canonical_masks(size) if mode == 'nesw-16' else [Image.new('L', (size, size), 255)] * 4
    cells = []
    for i, mask in enumerate(masks):
        cell = matched[i if mode == 'variants' else 0].convert('RGBA')
        cell.putalpha(mask)
        clean = Image.new('RGBA', (size, size))
        clean.alpha_composite(cell)
        cells.append(clean)
    sheet = Image.new('RGBA', (size * len(cells), size))
    for i, cell in enumerate(cells):
        sheet.paste(cell, (i * size, 0))
    alpha_hash = pixel_hash(sheet.getchannel('A'))
    if alpha_hash != ALPHA_SHA256[mode, size]:
        raise ValueError('canonical alpha differs from historical arrays')
    return sheet, material_reports, edge_contract(cells, mode), [
        {'index': i, 'variant' if mode == 'variants' else 'mask': VARIANTS[i] if mode == 'variants' else i,
         'pixelSha256': pixel_hash(cell), 'alphaSha256': pixel_hash(cell.getchannel('A'))}
        for i, cell in enumerate(cells)]


def plan(root, catalog):
    root = root.resolve()
    data = read_json(catalog)
    if (not isinstance(data, dict) or set(data) != {'schemaVersion', 'assets'}
            or type(data['schemaVersion']) is not int or data['schemaVersion'] != 1
            or not isinstance(data['assets'], dict)):
        raise ValueError('expected schemaVersion 1 and assets object')
    if any(topic not in TOPICS for topic in data['assets']):
        raise ValueError('unsupported canonical terrain topic; only explicitly reviewed material families are accepted')
    if not data['assets']:
        return []
    targets = validate_manifests(root, data['assets'])
    planned, sources = [], set()
    for topic, entry in data['assets'].items():
        mode = TOPICS[topic]
        source, provenance = read_material(root, entry, mode)
        if entry['sha256'] in sources:
            raise ValueError('each canonical topic requires its own authored material source')
        sources.add(entry['sha256'])
        for size, target in zip((32, 64), targets[topic]):
            sheet, samples, edges, cells = render_material(source, entry['registration']['windowsXYXY'], mode, size)
            report = {'topicId': topic, 'density': size // 32, 'tileMode': mode,
                      'output': str(target.relative_to(root)), 'size': list(sheet.size),
                      'source': provenance, 'samples': samples, 'cells': cells, 'edges': edges,
                      'pixelSha256': pixel_hash(sheet), 'alphaSha256': pixel_hash(sheet.getchannel('A')),
                      'derivation': 'explicit equal-scale source crops; independent LANCZOS resize at 32/64; '
                                    'approved outermost 1px opposite-family-edge average; exact historical alpha; '
                                    'transparent RGB zeroed; no recolor, contrast adjustment, blur or painted detail',
                      'integration': 'canonical-catalog-only; no current gameplay ground consumer',
                      'authoredMaterialCount': 1, 'derivedCellCount': len(cells), 'environmentCellCountIncrement': 0,
                      'visualReviewRequired': 'source identity and motif completeness at native 32px; '
                                              'repeated/mixed seams or connected topology fixtures; approved-v5 actor cohesion'}
            planned.append((target, sheet, report))
    return planned


def export(root, catalog, check=False):
    # Validate every source, registration, destination and rendered contract
    # before the first output write, including failures in later topics.
    planned = plan(root, catalog)
    for target, sheet, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != sheet.mode or current.size != sheet.size or current.tobytes() != sheet.tobytes():
                    raise ValueError(f'stale canonical terrain export: {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            sheet.save(target, format='PNG', optimize=True)
    return [report for _, _, report in planned]


def review_previews(sheet, mode):
    size = sheet.height
    count = 4 if mode == 'variants' else 16
    if mode not in ('variants', 'nesw-16') or size not in (32, 64) or sheet.size != (size * count, size):
        raise ValueError('unexpected canonical terrain preview sheet')
    if mode == 'variants':
        return seam_previews(sheet)
    cells = [sheet.crop((i * size, 0, (i + 1) * size, size)) for i in range(16)]
    all_masks = Image.new('RGBA', (size * 4, size * 4))
    for bits, cell in enumerate(cells):
        all_masks.paste(cell, ((bits % 4) * size, (bits // 4) * size))
    previews = {'all-masks-4x4': all_masks}
    for name, bits in (('isolated', 0), ('end', 1), ('straight-ns', 5), ('straight-ew', 10),
                       ('corner', 3), ('tee', 7), ('cross', 15)):
        fixture = Image.new('RGBA', (size * 3, size * 3))
        fixture.paste(cells[bits], (size, size))
        for bit, neighbor, position in ((1, 4, (1, 0)), (2, 8, (2, 1)), (4, 1, (1, 2)), (8, 2, (0, 1))):
            if bits & bit:
                fixture.paste(cells[neighbor], tuple(n * size for n in position))
        previews[name] = fixture
    return previews


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--preview-dir', type=Path)
    args = parser.parse_args()
    if args.check and args.preview_dir:
        parser.error('--preview-dir cannot be combined with --check')
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))
    if args.preview_dir:
        for _, sheet, report in plan(ROOT, ROOT / CATALOG):
            args.preview_dir.mkdir(parents=True, exist_ok=True)
            for name, preview in review_previews(sheet, report['tileMode']).items():
                preview.save(args.preview_dir / f'{report["topicId"].lower()}-{report["density"]}x-{name}.png')
