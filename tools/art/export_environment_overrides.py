#!/usr/bin/env python3
"""Deterministic scenery cell overlay onto immutable, checksummed atlas baselines."""
import argparse
import hashlib
import io
import json
from pathlib import Path
from PIL import Image, ImageOps
from export_overrides import ROOT, read_json, within, validate_alpha

PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')
CATALOG = Path('tools/art/environment-overrides.json')


def load_source(root, pack, entry):
    if not isinstance(entry, dict) or set(entry) != {'source', 'sha256'}:
        raise ValueError('expected source and sha256 only')
    path = within(root, entry['source'])
    if not path.is_relative_to((pack / 'masters').resolve()):
        raise ValueError('source must be in pack masters')
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != entry['sha256']:
        raise ValueError('source SHA-256 mismatch')
    with Image.open(io.BytesIO(raw)) as im:
        if im.format != 'PNG' or im.mode != 'RGBA':
            raise ValueError('expected PNG RGBA')
        return im.copy()


def render_cell(source, offset_y_2x=0):
    # Preserve complete authored canvas including alpha1 residue. No inferred crop.
    if type(offset_y_2x) is not int or not -10 <= offset_y_2x <= 10:
        raise ValueError('offsetY2x must be integer between -10 and 10')
    validate_alpha(source, 'environment master')
    fitted = ImageOps.contain(source, (172, 172), Image.Resampling.LANCZOS)
    hd = Image.new('RGBA', (192, 192))
    hd.paste(fitted, ((192 - fitted.width) // 2, 182 - fitted.height + offset_y_2x))
    logical = hd.resize((96, 96), Image.Resampling.LANCZOS)
    validate_alpha(hd, 'HD cell')
    validate_alpha(logical, 'logical cell')
    return logical, hd


def cell_box(index, size):
    x, y = index % 4 * size, index // 4 * size
    return x, y, x + size, y + size


def pixel_hash(image):
    return hashlib.sha256(image.tobytes()).hexdigest()


def plan(root, catalog):
    data = read_json(catalog)
    if set(data) != {'schemaVersion', 'atlases'} or data['schemaVersion'] != 1 or not isinstance(data['atlases'], dict):
        raise ValueError('expected schemaVersion1 and atlases object')
    pack = root / PACK
    env = pack / 'environment-builder-v1'
    manifest = read_json(env / 'manifest-environment-builder-v1.json')
    planned = []
    for atlas_id, override in data['atlases'].items():
        if atlas_id not in ('forest-temperate', 'forest-existing', 'scenery-regional'):
            raise ValueError('unsupported environment atlas')
        matches = [a for a in manifest['atlases'] if a['id'] == atlas_id]
        if len(matches) != 1:
            raise ValueError('atlas must have exactly one manifest record')
        atlas = matches[0]
        if (atlas['columns'], atlas['rows'], atlas['cellWidth'], atlas['cellHeight'],
            atlas['cellWidth2x'], atlas['cellHeight2x'], atlas['anchor'], atlas['tileMode']) != (4, 4, 96, 96, 192, 192, [48, 91], 'modules'):
            raise ValueError('unsupported scenery contract')
        order = atlas['cellOrder']
        if len(order) != 16 or len(set(order)) != 16 or [c['id'] for c in atlas['cells']] != order or any(c['anchor'] != [48, 91] for c in atlas['cells']):
            raise ValueError('inconsistent cell order/anchors')
        if not isinstance(override, dict) or set(override) != {'baselines', 'cells'} or set(override['baselines']) != {'1x', '2x'} or not isinstance(override['cells'], dict):
            raise ValueError('expected baselines 1x/2x and cells')
        baselines = [load_source(root, pack, override['baselines'][k]) for k in ('1x', '2x')]
        if [im.size for im in baselines] != [(384, 384), (768, 768)]:
            raise ValueError('incorrect baseline atlas dimensions')
        outputs = [im.copy() for im in baselines]
        changed = set()
        sources = {}
        for cell_id, entry in override['cells'].items():
            if cell_id not in order:
                raise ValueError('unknown environment cell')
            index = order.index(cell_id)
            if not isinstance(entry, dict) or set(entry) not in ({'source', 'sha256'}, {'source', 'sha256', 'offsetY2x'}):
                raise ValueError('cell expects source, sha256 and optional offsetY2x')
            source = load_source(root, pack, {k: entry[k] for k in ('source', 'sha256')})
            offset = entry.get('offsetY2x', 0)
            cells = render_cell(source, offset)
            sources[cell_id] = {'sourceSha256': entry['sha256'], 'sourceAlpha': validate_alpha(source, cell_id), 'offsetY2x': offset}
            for output, cell, size in zip(outputs, cells, (96, 192)):
                output.paste(cell, cell_box(index, size)[:2])
            changed.add(index)
        for density, base, output, size, key in zip((1, 2), baselines, outputs, (96, 192), ('png', 'png2x')):
            target = within(env, atlas[key])
            if target != (env / 'runtime/atlas' / f'{atlas_id}{"@2x" if density == 2 else ""}.png').resolve():
                raise ValueError('unexpected atlas destination')
            cells = []
            for i, cell_id in enumerate(order):
                before = pixel_hash(base.crop(cell_box(i, size)))
                after = pixel_hash(output.crop(cell_box(i, size)))
                if i not in changed and before != after:
                    raise ValueError('unaffected cell changed')
                cells.append({'id': cell_id, 'index': i, 'overridden': i in changed,
                              'baselinePixelSha256': before, 'outputPixelSha256': after})
            report = {'atlasId': atlas_id, 'density': density, 'output': str(target.relative_to(root)),
                      'baseline': override['baselines'][f'{density}x'], 'sources': sources,
                      'pixelSha256': pixel_hash(output), 'cells': cells}
            planned.append((target, output, report))
    return planned


def export(root, catalog, check=False):
    planned = plan(root, catalog)
    for target, output, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != 'RGBA' or current.size != output.size or current.tobytes() != output.tobytes():
                    raise ValueError(f'stale environment export: {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            output.save(target, format='PNG', optimize=True)
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))
