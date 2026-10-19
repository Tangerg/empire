"""Retain the existing stone connector geometry as white RGB plus unchanged alpha."""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image

from export_overrides import ROOT, read_json, within
from export_environment_overrides import PACK

MANIFEST = PACK / 'world-materials-v1/manifest-stone-connectors-v1.json'
ATLAS_IDS = ('route-stone-road', 'route-edge-stone-road')


def plan(root: Path, manifest: Path):
    document = read_json(manifest)
    if set(document) != {'schemaVersion', 'atlases'} or document['schemaVersion'] != 1:
        raise ValueError('expected stone connector schemaVersion 1')
    if not isinstance(document['atlases'], list) or [a.get('id') for a in document['atlases']] != list(ATLAS_IDS):
        raise ValueError('expected exactly stone road and stone edge in existing layer order')
    environment = root / PACK / 'environment-builder-v1'
    catalog = read_json(environment / 'manifest-environment-builder-v1.json')
    planned = []
    for spec in document['atlases']:
        if set(spec) != {'id', 'sourceSha256', 'sourceSha2562x', 'png', 'png2x'}:
            raise ValueError('expected explicit source hashes and technical mask output paths')
        matches = [a for a in catalog['atlases'] if a['id'] == spec['id']]
        if len(matches) != 1:
            raise ValueError('stone connector must resolve one existing atlas')
        atlas = matches[0]
        if (atlas['columns'], atlas['rows'], atlas['cellWidth'], atlas['cellHeight'],
                atlas['cellWidth2x'], atlas['cellHeight2x'], atlas['componentCount']) != (16, 4, 32, 32, 64, 64, 64):
            raise ValueError('existing stone connector geometry changed')
        if (atlas['tileMode'] != 'nesw-16-variants' or atlas['maskOrder'] != list(range(16))
                or atlas['variantsPerMask'] != 4 or atlas['bitOrder'] != ['N', 'E', 'S', 'W']
                or atlas['cellIndex'] != 'mask * 4 + variant'):
            raise ValueError('existing stone connector semantic order changed')
        for density, key, hash_key in [(1, 'png', 'sourceSha256'), (2, 'png2x', 'sourceSha2562x')]:
            source = within(environment, atlas[key])
            raw = source.read_bytes()
            if hashlib.sha256(raw).hexdigest() != spec[hash_key]:
                raise ValueError(f'{spec["id"]}: source SHA-256 mismatch')
            with Image.open(io.BytesIO(raw)) as opened:
                if opened.format != 'PNG' or opened.mode != 'RGBA' or opened.size != (512 * density, 128 * density):
                    raise ValueError('expected existing RGBA stone connector dimensions')
                alpha = opened.getchannel('A')
            image = Image.new('RGBA', alpha.size, (255, 255, 255, 255))
            image.putalpha(alpha)
            target = within(manifest.parent, spec[key])
            if target.parent != (manifest.parent / 'runtime').resolve() or target.suffix != '.png':
                raise ValueError('stone technical mask must be a PNG in world runtime')
            cell = 32 * density
            report = {'atlas': spec['id'], 'source': str(source.relative_to(root)),
                      'sourceSha256': spec[hash_key], 'output': str(target.relative_to(root)),
                      'size': list(image.size), 'density': density,
                      'alphaSha256': hashlib.sha256(alpha.tobytes()).hexdigest(),
                      'alphaRange': list(alpha.getextrema()),
                      'cellAlphaSha256': [hashlib.sha256(alpha.crop((index % 16 * cell, index // 16 * cell,
                          (index % 16 + 1) * cell, (index // 16 + 1) * cell)).tobytes()).hexdigest() for index in range(64)],
                      'derivation': 'white technical RGB; all original alpha bytes retained; no resize or threshold'}
            planned.append((target, image, report))
    if len({p for p, _, _ in planned}) != len(planned):
        raise ValueError('technical mask output paths must be distinct')
    return planned


def export(root: Path, manifest: Path, check=False):
    planned = plan(root, manifest)
    for target, image, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != image.mode or current.size != image.size or current.tobytes() != image.tobytes():
                    raise ValueError(f'stale stone technical mask: {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target, optimize=True)
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, default=MANIFEST)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / args.manifest, args.check), indent=2))
