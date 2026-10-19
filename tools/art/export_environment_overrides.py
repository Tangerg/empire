#!/usr/bin/env python3
"""Deterministic scenery cell overlay onto immutable, checksummed atlas baselines."""
import argparse
import hashlib
import io
import json
import math
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, read_json, within, validate_alpha

PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')
CATALOG = Path('tools/art/environment-overrides.json')

# This legacy sheet names its slots differently from its semantic cells. Keep
# the exact paired order; neither labels nor runtime indices may be rewritten.
TEMPERATE_CLIFF_SLOTS = [
    ('straight-south', 'temperate-cliff-straight-south'),
    ('end-west', 'temperate-cliff-end-west'),
    ('end-east', 'temperate-cliff-end-east'),
    ('concave-corner', 'temperate-cliff-concave-corner'),
    ('hill-cap', 'temperate-hill-cap'),
    ('ramp-level-1', 'temperate-ramp-level-1'),
    ('ramp-level-2', 'temperate-ramp-level-2'),
    ('mountain-pass', 'temperate-mountain-pass'),
    ('convex-corner', 'temperate-cliff-convex-corner'),
    ('broken-ledge', 'temperate-broken-ledge'),
    ('low-ledge', 'temperate-low-ledge'),
    ('talus-foot', 'temperate-talus-foot'),
    ('pillar-wide', 'temperate-pillar-wide'),
    ('pillar-narrow', 'temperate-pillar-narrow'),
    ('ledge-end', 'temperate-ledge-end'),
    ('plateau-cap', 'temperate-plateau-cap'),
]

FORTIFICATION_SLOTS = [
    'wood-bridge-horizontal', 'wood-bridge-vertical',
    'stone-bridge-horizontal', 'stone-bridge-vertical',
    'wood-bridgehead-west', 'wood-bridgehead-east',
    'palisade-straight', 'palisade-corner', 'palisade-gate', 'palisade-breach',
    'stone-wall-straight', 'stone-wall-corner', 'stone-gatehouse',
    'stone-wall-breach', 'watchtower-wood', 'wood-barricade',
]

# This one catalog-only sheet has no semantic cell records. These are original
# slot labels, not runtime cell IDs; exporting it must not invent cell metadata.
HIGHLAND_SLOT_LABELS = [
    'straight-south', 'end-west', 'end-east', 'concave-corner',
    'hill-cap', 'ramp-level-1', 'ramp-level-2', 'mountain-pass',
    'convex-corner', 'broken-ledge', 'low-ledge', 'talus-foot',
    'pillar-wide', 'pillar-narrow', 'ledge-end', 'plateau-cap',
]


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


def finite_number(value):
    return type(value) in (int, float) and math.isfinite(value)


def point(value):
    return isinstance(value, list) and len(value) == 2 and all(finite_number(n) for n in value)


def render_cell(source, registration, cell_size=96):
    # One explicit full-canvas transform, including alpha1 residue. No inferred crop.
    if not isinstance(registration, dict) or set(registration) != {'scale', 'origin', 'sourceFoot'}:
        raise ValueError('registration requires scale, origin and sourceFoot')
    scale, origin, foot = (registration[key] for key in ('scale', 'origin', 'sourceFoot'))
    if not finite_number(scale) or scale <= 0 or not point(origin) or not point(foot):
        raise ValueError('registration must contain a positive scale and finite coordinate pairs')
    if any(n < 0 or not (n * 2).is_integer() for n in map(float, origin)):
        raise ValueError('registration origin must use nonnegative half-logical-pixel steps')
    if any(foot[i] < 0 or foot[i] > source.size[i] for i in (0, 1)):
        raise ValueError('sourceFoot must lie on the authored canvas')
    size = tuple(round(n * scale * 2) for n in source.size)
    origin_hd = tuple(round(n * 2) for n in origin)
    if any(size[i] < 1 or origin_hd[i] + size[i] > cell_size * 2 for i in (0, 1)):
        raise ValueError('registration would clip the full authored canvas')
    validate_alpha(source, 'environment master')
    fitted = source.resize(size, Image.Resampling.LANCZOS)
    hd = Image.new('RGBA', (cell_size * 2, cell_size * 2))
    hd.paste(fitted, origin_hd)
    logical = hd.resize((cell_size, cell_size), Image.Resampling.LANCZOS)
    validate_alpha(hd, 'HD cell')
    validate_alpha(logical, 'logical cell')
    # Raster dimensions are rounded once at HD density; the pivot follows that
    # actual transform, including on nonsquare authored canvases.
    anchor = [origin[i] + foot[i] * size[i] / (2 * source.size[i]) for i in (0, 1)]
    return logical, hd, anchor


def cell_box(index, size, columns=4):
    x, y = index % columns * size, index // columns * size
    return x, y, x + size, y + size


def pixel_hash(image):
    return hashlib.sha256(image.tobytes()).hexdigest()


def plan(root, catalog):
    data = read_json(catalog)
    if set(data) != {'schemaVersion', 'atlases'} or data['schemaVersion'] != 2 or not isinstance(data['atlases'], dict):
        raise ValueError('expected schemaVersion2 and atlases object')
    pack = root / PACK
    env = pack / 'environment-builder-v1'
    manifest_path = env / 'manifest-environment-builder-v1.json'
    manifest = read_json(manifest_path)
    planned = []
    contracts = {
        'forest-temperate': (4, 96, [48, 91]),
        'forest-existing': (4, 96, [48, 91]),
        'scenery-regional': (4, 96, [48, 91]),
        'rural-life': (4, 112, [56, 108]),
        'decals-small': (6, 64, [32, 60]),
        'camps-foundations': (4, 144, [72, 139]),
        'landmarks-large': (4, 160, [80, 155]),
        'cliff-modules-temperate': (4, 128, [64, 127]),
        'cliff-modules-highland': (4, 128, [64, 127]),
        'crossings-fortifications': (4, 96, [48, 91]),
    }
    for atlas_id, override in data['atlases'].items():
        if atlas_id not in contracts:
            raise ValueError('unsupported environment atlas')
        matches = [a for a in manifest['atlases'] if a['id'] == atlas_id]
        if len(matches) != 1:
            raise ValueError('atlas must have exactly one manifest record')
        atlas = matches[0]
        columns, cell_size, default_anchor = contracts[atlas_id]
        cell_count = columns * 4
        if (atlas['columns'], atlas['rows'], atlas['cellWidth'], atlas['cellHeight'],
            atlas['cellWidth2x'], atlas['cellHeight2x'], atlas['anchor'], atlas['tileMode'],
            atlas['componentCount']) != (columns, 4, cell_size, cell_size, cell_size * 2,
                                        cell_size * 2, default_anchor, 'modules', cell_count):
            raise ValueError('unsupported scenery contract')
        cell_sizes = (atlas['cellWidth'], atlas['cellWidth2x'])
        labels = atlas['cellOrder']
        index_only = atlas_id == 'cliff-modules-highland'
        if index_only:
            if (atlas['cells'] != [] or labels != HIGHLAND_SLOT_LABELS
                    or atlas.get('category') != 'elevation-module'
                    or atlas.get('ySort') != 'bottom-anchor'
                    or atlas.get('heightDeltaRange') != [1, 2]
                    or atlas.get('rampCells') != [5, 6, 7]
                    or atlas.get('blockingByDefault') is not True):
                raise ValueError('inconsistent highland index-only contract')
            order = labels
        else:
            order = [c['id'] for c in atlas['cells']]
        if (len(labels) != cell_count or len(set(labels)) != cell_count
                or len(order) != cell_count or len(set(order)) != cell_count):
            raise ValueError('inconsistent cell order')
        if atlas_id == 'cliff-modules-temperate':
            if list(zip(labels, order)) != TEMPERATE_CLIFF_SLOTS:
                raise ValueError('inconsistent cliff label/semantic cell order')
        elif atlas_id == 'crossings-fortifications':
            if (labels != FORTIFICATION_SLOTS or order != FORTIFICATION_SLOTS
                    or any(type(cell.get('index', index)) is not int
                           or cell.get('index', index) != index
                           for index, cell in enumerate(atlas['cells']))):
                raise ValueError('inconsistent fortification cell order')
        elif labels != order:
            raise ValueError('inconsistent cell order')
        for cell in atlas['cells']:
            if not point(cell['anchor']):
                raise ValueError('cell anchor must be a finite coordinate pair')
            presentation_scale = cell.get('presentationScale', 1)
            if not finite_number(presentation_scale) or presentation_scale <= 0:
                raise ValueError('cell presentationScale must be finite and positive')
        if not isinstance(override, dict) or set(override) != {'baselines', 'cells'} or set(override['baselines']) != {'1x', '2x'} or not isinstance(override['cells'], dict):
            raise ValueError('expected baselines 1x/2x and cells')
        baselines = [load_source(root, pack, override['baselines'][k]) for k in ('1x', '2x')]
        if [im.size for im in baselines] != [(size * atlas['columns'], size * atlas['rows']) for size in cell_sizes]:
            raise ValueError('incorrect baseline atlas dimensions')
        outputs = [im.copy() for im in baselines]
        changed = set()
        sources = {}
        for cell_id, entry in override['cells'].items():
            if cell_id not in order:
                raise ValueError('unknown environment cell')
            index = order.index(cell_id)
            if not isinstance(entry, dict) or set(entry) != {'source', 'sha256', 'registration'}:
                raise ValueError('cell expects source, sha256 and registration')
            source = load_source(root, pack, {k: entry[k] for k in ('source', 'sha256')})
            logical, hd, anchor = render_cell(source, entry['registration'], cell_sizes[0])
            sources[cell_id] = {
                'sourceSha256': entry['sha256'], 'sourceAlpha': validate_alpha(source, cell_id),
                'registration': entry['registration'],
            }
            if index_only:
                if (entry['registration']['origin'] != [2, 2]
                        or not math.isclose(entry['registration']['scale'], 124 / max(source.size),
                                            rel_tol=1e-12, abs_tol=0)):
                    raise ValueError('highland requires complete canvas fitted to 124 logical pixels at [2, 2]')
                sources[cell_id].update(logicalAnchor=list(default_anchor), sourceFootLogical=anchor,
                                        contactCalibrated=False)
            else:
                metadata = atlas['cells'][index]
                if any(not math.isclose(metadata['anchor'][i], anchor[i], abs_tol=1e-10) for i in (0, 1)):
                    metadata['anchor'] = anchor
                sources[cell_id].update(logicalAnchor=metadata['anchor'],
                                        presentationScale=metadata.get('presentationScale', 1))
            for output, cell, size in zip(outputs, (logical, hd), cell_sizes):
                output.paste(cell, cell_box(index, size, columns)[:2])
            changed.add(index)
        for density, base, output, size, key in zip((1, 2), baselines, outputs, cell_sizes, ('png', 'png2x')):
            target = within(env, atlas[key])
            if target != (env / 'runtime/atlas' / f'{atlas_id}{"@2x" if density == 2 else ""}.png').resolve():
                raise ValueError('unexpected atlas destination')
            cells = []
            for i, cell_id in enumerate(order):
                before = pixel_hash(base.crop(cell_box(i, size, columns)))
                after = pixel_hash(output.crop(cell_box(i, size, columns)))
                if i not in changed and before != after:
                    raise ValueError('unaffected cell changed')
                cells.append({'label' if index_only else 'id': cell_id, 'index': i, 'overridden': i in changed,
                              'baselinePixelSha256': before, 'outputPixelSha256': after})
            report = {'atlasId': atlas_id, 'density': density, 'output': str(target.relative_to(root)),
                      'baseline': override['baselines'][f'{density}x'], 'sources': sources,
                      'pixelSha256': pixel_hash(output), 'cells': cells}
            if index_only:
                report.update(cellAddressing='original-slot-label-and-index',
                              anchorPolicy='atlas-default-preserved', integration='catalog-only')
            planned.append((target, output, report))
    return planned, manifest_path, manifest


def export(root, catalog, check=False):
    planned, manifest_path, manifest = plan(root, catalog)
    metadata_changed = read_json(manifest_path) != manifest
    if check and metadata_changed:
        raise ValueError('stale environment registration anchors')
    for target, output, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != 'RGBA' or current.size != output.size or current.tobytes() != output.tobytes():
                    raise ValueError(f'stale environment export: {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            output.save(target, format='PNG', optimize=True)
    if not check and metadata_changed:
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))
