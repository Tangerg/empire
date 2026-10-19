#!/usr/bin/env python3
"""Replace transition material while preserving immutable legacy alpha masks."""
import argparse
import hashlib
import io
import json
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, read_json, within
from export_environment_overrides import PACK, load_source
from export_surface_overrides import match_edges, seam_report

CATALOG = Path('tools/art/transition-overrides.json')
MASKS = [0, 1, 4, 5, 7, 16, 17, 20, 21, 23, 28, 29, 31, 64, 65, 68, 69, 71, 80, 81, 84, 85, 87, 92, 93, 95, 112, 113, 116, 117, 119, 124, 125, 127, 193, 197, 199, 209, 213, 215, 221, 223, 241, 245, 247, 253, 255]


def plan(root, catalog):
    data = read_json(catalog)
    if set(data) != {'schemaVersion', 'atlases'} or data['schemaVersion'] != 1 or not isinstance(data['atlases'], dict):
        raise ValueError('expected schemaVersion1 and atlases')
    pack = root / PACK
    env = pack / 'environment-builder-v1'
    manifest = read_json(env / 'manifest-environment-builder-v1.json')
    planned = []
    for name, override in data['atlases'].items():
        if name != 'transition-meadow-forest':
            raise ValueError('unsupported transition')
        matches = [a for a in manifest['atlases'] if a['id'] == name]
        if len(matches) != 1:
            raise ValueError('expected one transition manifest')
        a = matches[0]
        if (a['columns'], a['rows'], a['cellWidth'], a['cellHeight'], a['cellWidth2x'], a['cellHeight2x'], a['componentCount'], a['tileMode'], a['maskOrder'], a['bitOrder'], a['baseSurface'], a['overlaySurface']) != (16, 3, 32, 32, 64, 64, 47, 'blob-47', MASKS, ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'], 'surface-meadow', 'surface-forest-floor'):
            raise ValueError('unexpected transition contract')
        if not isinstance(override, dict) or set(override) != {'source', 'sha256', 'baselines'} or set(override['baselines']) != {'1x', '2x'}:
            raise ValueError('expected source, sha256 and immutable baselines')
        path = within(root, override['source'])
        if not path.is_relative_to((pack / 'masters').resolve()):
            raise ValueError('material source must be in own pack masters')
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != override['sha256']:
            raise ValueError('source SHA-256 mismatch')
        with Image.open(io.BytesIO(raw)) as opened:
            if opened.format != 'PNG' or opened.mode not in ('RGB', 'RGBA') or opened.width != opened.height or opened.width < 64:
                raise ValueError('expected square opaque material PNG')
            if opened.mode == 'RGBA' and opened.getchannel('A').getextrema() != (255, 255):
                raise ValueError('material source must be opaque')
            source = opened.convert('RGB')
        for density, size, key in ((1, 32, 'png'), (2, 64, 'png2x')):
            baseline_entry = override['baselines'][f'{density}x']
            baseline = load_source(root, pack, baseline_entry)
            if baseline.size != (size * 16, size * 3):
                raise ValueError('incorrect transition baseline size')
            before = source.resize((size, size), Image.Resampling.LANCZOS)
            texture = match_edges([before] * 4)[0]
            output = baseline.copy()
            mask_reports = []
            for i, mask in enumerate(MASKS):
                x, y = i % 16 * size, i // 16 * size
                alpha = baseline.crop((x, y, x + size, y + size)).getchannel('A')
                cell = texture.convert('RGBA')
                cell.putalpha(alpha)
                output.paste(cell, (x, y))
                mask_reports.append({'index': i, 'mask': mask, 'alphaSha256': hashlib.sha256(alpha.tobytes()).hexdigest()})
            if output.getchannel('A').tobytes() != baseline.getchannel('A').tobytes():
                raise ValueError('transition alpha geometry changed')
            target = within(env, a[key])
            if target != (env / 'runtime/atlas' / f'{name}{"@2x" if density == 2 else ""}.png').resolve():
                raise ValueError('unexpected transition destination')
            unused_box = (15 * size, 2 * size, 16 * size, 3 * size)
            if output.crop(unused_box).tobytes() != baseline.crop(unused_box).tobytes():
                raise ValueError('unused transition cell changed')
            planned.append((target, output, {
                'atlasId': name, 'density': density, 'output': str(target.relative_to(root)), 'size': list(output.size),
                'source': override['source'], 'sourceSha256': override['sha256'], 'baseline': baseline_entry,
                'materialDerivation': seam_report(before, texture), 'masks': mask_reports,
                'alphaIdenticalToBaseline': True, 'unusedCellPreserved': True,
                'pixelSha256': hashlib.sha256(output.tobytes()).hexdigest(),
                'visualTransitionReview': 'required-separately'}))
    return planned


def export(root, catalog, check=False):
    planned = plan(root, catalog)
    for target, image, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != 'RGBA' or current.size != image.size or current.tobytes() != image.tobytes():
                    raise ValueError('stale transition export')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target, optimize=True)
    return [r for _, _, r in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))
