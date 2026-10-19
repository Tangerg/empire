#!/usr/bin/env python3
"""Export authored ground-edge cutouts without cropping or changing their alpha."""
import argparse
import hashlib
import io
import json
from PIL import Image
from export_overrides import ROOT, read_json, within, validate_alpha
from export_environment_overrides import PACK, finite_number, load_source, point

MANIFEST = PACK / 'edge-decoration-v1/manifest-edge-decoration-v1.json'


def plan(root, manifest):
    data = read_json(manifest)
    if data['schemaVersion'] != 1 or data['meaningfulAlphaMin'] != 2:
        raise ValueError('unsupported edge decoration contract')
    for dependency in data['geometryDependencies']:
        if hashlib.sha256(within(root, dependency['source']).read_bytes()).hexdigest() != dependency['sha256']:
            raise ValueError('edge placement geometry dependency changed; review layout again')
    planned = []
    for asset in data['assets']:
        source = load_source(root, root / PACK, {
            'source': str(PACK / asset['source']), 'sha256': asset['sourceSha256'],
        })
        scale, anchor = asset['logicalPerSourcePixel'], asset['sourceAnchor']
        if list(source.size) != asset['sourceSize']:
            raise ValueError('edge source canvas dimensions changed')
        if not finite_number(scale) or scale <= 0 or not point(anchor) or any(
                anchor[i] < 0 or anchor[i] > source.size[i] for i in (0, 1)):
            raise ValueError('invalid edge source registration')
        source_alpha = validate_alpha(source, asset['id'])
        bounds = source.getchannel('A').point(lambda a: 255 if a >= data['meaningfulAlphaMin'] else 0).getbbox()
        if list(bounds) != asset['meaningfulSourceBounds']:
            raise ValueError('edge meaningful alpha bounds changed')
        padding = asset['sourcePadding']
        if type(padding) is not int or padding < 0 or padding * scale > 4:
            raise ValueError('invalid technical transparent edge margin')
        # Preserve the faint authored perimeter inside the resampling canvas,
        # rather than clipping Lanczos support or erasing its resulting alpha.
        padded = Image.new('RGBA', tuple(n + 2 * padding for n in source.size))
        padded.paste(source, (padding, padding))
        logical_size = [n * scale for n in padded.size]
        for density, key in ((1, 'png'), (2, 'png2x')):
            size = tuple(round(n * density) for n in logical_size)
            if any(n <= 0 or n > padded.size[i] for i, n in enumerate(size)):
                raise ValueError('edge export requires native source detail without upscaling')
            output = padded.resize(size, Image.Resampling.LANCZOS)
            alpha = validate_alpha(output, f'{asset["id"]}@{density}x')
            target = within(manifest.parent, asset[key])
            expected = manifest.parent / 'runtime' / f'{asset["id"]}{"@2x" if density == 2 else ""}.png'
            if target != expected.resolve():
                raise ValueError('unexpected edge export destination')
            buffer = io.BytesIO()
            output.save(buffer, format='PNG')
            raw = buffer.getvalue()
            planned.append((target, raw, output, {
                'id': asset['id'], 'density': density, 'sourceSha256': asset['sourceSha256'],
                'sourceAlpha': source_alpha, 'sourceSize': asset['sourceSize'],
                'sourceAnchor': anchor, 'sourcePadding': padding,
                'logicalAnchor': [(n + padding) * scale for n in anchor],
                'logicalSize': logical_size, 'rasterSize': list(size),
                'actualPixelsPerWorldUnit': [size[i] / logical_size[i] for i in (0, 1)],
                'output': str(target.relative_to(root)), 'sha256': hashlib.sha256(raw).hexdigest(),
                'alphaSha256': hashlib.sha256(output.getchannel('A').tobytes()).hexdigest(),
                'alpha': alpha, 'meaningfulAlphaMin': data['meaningfulAlphaMin'],
                'derivation': 'complete authored RGBA canvas plus transparent margin, independent Lanczos resize only; no crop, mask, edge matching, opacity change or alpha painting',
            }))
    if len({target for target, _, _, _ in planned}) != len(planned):
        raise ValueError('duplicate edge export destination')
    return planned


def export(root, manifest, check=False):
    planned = plan(root, manifest)
    for target, raw, _, _ in planned:
        if check:
            if target.read_bytes() != raw:
                raise ValueError('stale edge decoration export')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
    return [report for _, _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / MANIFEST, args.check), indent=2))
