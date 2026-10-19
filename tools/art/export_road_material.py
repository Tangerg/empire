#!/usr/bin/env python3
"""Export approved earth unchanged and the deterministic continuous-road masks."""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image

from export_environment_overrides import PACK
from export_overrides import ROOT, read_json, within
from road_area import BIT_ORDER, CONCAVE_RADIUS, CONVEX_RADIUS, GEOMETRY, INSET, SUPERSAMPLE, TILE_SIZE, road_area_mask


MANIFEST = PACK / 'world-materials-v1/manifest-road-material-v2.json'
SOURCE = 'masters/approved-v5/earth-material/revision-2/master.png'
SOURCE_SHA256 = '9afeebdb434aed8fe30459db2dbf457173c3b0d49fad2a966dbb8837718dbd53'
OCCUPANCY = {
    'tileSize': TILE_SIZE, 'columns': 16, 'rows': 16, 'bitOrder': BIT_ORDER,
    'geometry': GEOMETRY, 'inset': INSET, 'convexRadius': CONVEX_RADIUS, 'concaveRadius': CONCAVE_RADIUS,
    'png': 'runtime/road-masks.png', 'png2x': 'runtime/road-masks@2x.png',
}


def pixel_hash(image):
    return hashlib.sha256(image.tobytes()).hexdigest()


def plan(root, manifest):
    root, manifest = Path(root).resolve(), Path(manifest).resolve()
    data = read_json(manifest)
    required = {'schemaVersion', 'id', 'replaces', 'source', 'sourceSha256', 'logicalSize', 'rasterSize',
                'worldOrigin', 'fit', 'png', 'png2x', 'occupancy'}
    if set(data) != required or data['schemaVersion'] != 2 or data['id'] != 'road-earth':
        raise ValueError('expected road-earth material schemaVersion 2')
    if data['replaces'] != {'atlas': 'route-dirt-road', 'mask': 'route'}:
        raise ValueError('replacement must identify the approved route-dirt-road material')
    if data['logicalSize'] != [1024, 1024] or data['rasterSize'] != [512, 512]:
        raise ValueError('road requires a 1024-unit world period and 512/1024px independent exports')
    if data['worldOrigin'] != [0, 0] or data['fit'] != {'inset': 0, 'bleed': 0}:
        raise ValueError('road requires zero origin and an uncropped fit')
    if data['occupancy'] != OCCUPANCY:
        raise ValueError('road requires the complete approved eight-neighbor occupancy geometry')
    if data['source'] != SOURCE or data['sourceSha256'] != SOURCE_SHA256:
        raise ValueError('road source must retain the approved earth path and SHA-256')
    pack = root / PACK
    environment = read_json(pack / 'environment-builder-v1/manifest-environment-builder-v1.json')
    if sum(atlas['id'] == data['replaces']['atlas'] for atlas in environment['atlases']) != 1:
        raise ValueError('replacement must identify one existing road atlas')
    source = within(pack, data['source'])
    if not source.is_relative_to((pack / 'masters').resolve()):
        raise ValueError('road source must live under pack masters')
    raw = source.read_bytes()
    if hashlib.sha256(raw).hexdigest() != SOURCE_SHA256:
        raise ValueError('road source SHA-256 mismatch')
    with Image.open(io.BytesIO(raw)) as opened:
        if opened.format != 'PNG' or opened.mode not in ('RGB', 'RGBA') or opened.size != (1254, 1254):
            raise ValueError('expected the opaque 1254px approved RGB/RGBA PNG')
        if opened.mode == 'RGBA' and opened.getchannel('A').getextrema() != (255, 255):
            raise ValueError('road source must be fully opaque')
        image = opened.convert('RGB')

    def output_path(relative):
        target = within(manifest.parent, relative)
        if target.parent != (manifest.parent / 'runtime').resolve() or target.suffix != '.png':
            raise ValueError('road outputs must be PNGs in the manifest runtime directory')
        return target

    planned = []
    for density, key in ((1, 'png'), (2, 'png2x')):
        target = output_path(data[key])
        dimensions = (512 * density, 512 * density)
        # No crop, edge averaging, color processing, or resize of another export.
        exported = image.resize(dimensions, Image.Resampling.LANCZOS)
        planned.append((target, exported, {
            'id': data['id'], 'source': str(source.relative_to(root)), 'sourceSha256': SOURCE_SHA256,
            'sourceSize': list(image.size), 'output': str(target.relative_to(root)), 'exportDensity': density,
            'size': list(dimensions), 'logicalSize': data['logicalSize'], 'rasterSize': data['rasterSize'],
            'pixelsPerWorldUnit': [density / 2, density / 2], 'worldOrigin': data['worldOrigin'],
            'fit': data['fit'], 'replaces': data['replaces'], 'edgeBandPixels': 0,
            'derivation': 'independent uncropped full-source Lanczos resize; no painting, recolor or edge averaging',
            'pixelSha256': pixel_hash(exported),
        }))
    for density, key in ((1, 'png'), (2, 'png2x')):
        cell_size = TILE_SIZE * density
        atlas = Image.new('RGBA', (cell_size * 16, cell_size * 16), (255, 255, 255, 0))
        for mask in range(256):
            cell = Image.new('RGBA', (cell_size, cell_size), (255, 255, 255, 255))
            cell.putalpha(road_area_mask(cell_size, mask))
            atlas.paste(cell, (mask % 16 * cell_size, mask // 16 * cell_size))
        target = output_path(data['occupancy'][key])
        planned.append((target, atlas, {
            'id': 'road-occupancy-masks', 'output': str(target.relative_to(root)), 'exportDensity': density,
            'size': list(atlas.size), 'logicalCellSize': TILE_SIZE, 'geometry': GEOMETRY,
            'inset': INSET, 'convexRadius': CONVEX_RADIUS, 'concaveRadius': CONCAVE_RADIUS,
            'bitOrder': BIT_ORDER, 'supersamplePerWorldUnit': SUPERSAMPLE,
            'derivation': 'analytic bounded contour on one common 16x grid; independent integer area integration',
            'pixelSha256': pixel_hash(atlas), 'source': 'technical white-alpha occupancy geometry; no painted art',
        }))
    if len({target for target, _, _ in planned}) != len(planned):
        raise ValueError('material and mask outputs must have distinct paths')
    return planned


def export(root, manifest, check=False):
    planned = plan(root, manifest)
    for target, image, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != image.mode or current.size != image.size or current.tobytes() != image.tobytes():
                    raise ValueError(f'stale road material export: {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target, optimize=True)
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--manifest', type=Path, default=MANIFEST)
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / args.manifest, args.check), indent=2))
