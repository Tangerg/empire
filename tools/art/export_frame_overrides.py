#!/usr/bin/env python3
"""Pack reviewed, distinct semantic poses with one explicit shared transform."""
import argparse
import hashlib
import io
import json
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, read_json, within, validate_alpha, records

CATALOG = Path('tools/art/frame-overrides.json')
ORDERS = {
    'combat-unit': ['idle', 'walk-a', 'attack', 'walk-b'],
    'mission-unit': ['idle', 'walk-a', 'carry-or-work', 'walk-b'],
    'interactive-structure': ['normal', 'damaged', 'captured'],
}


def plan(root, catalog):
    data = read_json(catalog)
    if set(data) != {'schemaVersion', 'assets'} or data['schemaVersion'] != 1 or not isinstance(data['assets'], dict):
        raise ValueError('expected schemaVersion1 and assets object')
    known = records(root)
    planned = []
    targets = set()
    for topic, override in data['assets'].items():
        if topic not in known:
            raise ValueError('unknown topic')
        pack, asset, hd = known[topic]
        category = asset['category']
        if category not in ORDERS:
            raise ValueError('unsupported framed category')
        order = ORDERS[category]
        fw, fh = asset['frameWidth'], asset['frameHeight']
        if (fw, fh) not in ((32, 48), (64, 48), (64, 64), (96, 64)):
            raise ValueError('unsupported frame dimensions')
        if category == 'interactive-structure' and (fw, fh) != (64, 64):
            raise ValueError('unsupported structure dimensions')
        if (asset['width'], asset['height'], asset['frames'], asset['anchor']) != (fw * len(order), fh, len(order), [fw // 2, fh - 1]) or asset.get('stateOrder' if category == 'interactive-structure' else 'frameOrder') != order:
            raise ValueError('inconsistent frame contract')
        if not isinstance(override, dict) or set(override) != {'kind', 'frames', 'placement'} or override['kind'] != 'frames-v1' or not isinstance(override['frames'], dict) or set(override['frames']) != set(order):
            raise ValueError('exact semantic frame set required')
        placement = override['placement']
        if not isinstance(placement, dict) or set(placement) != {'renderWidth', 'origin'}:
            raise ValueError('expected renderWidth and origin')
        width, origin = placement['renderWidth'], placement['origin']
        if type(width) is not int or width < 1 or not isinstance(origin, list) or len(origin) != 2 or any(type(n) is not int or n < 0 for n in origin):
            raise ValueError('placement must use positive width and nonnegative integer origin')
        sources, provenance, hashes = [], [], set()
        source_size = None
        for name in order:
            entry = override['frames'][name]
            if not isinstance(entry, dict) or set(entry) != {'source', 'sha256'}:
                raise ValueError('frame expects source and sha256')
            path = within(root, entry['source'])
            if not path.is_relative_to((pack / 'masters').resolve()):
                raise ValueError('source must be in own pack masters')
            raw = path.read_bytes()
            if hashlib.sha256(raw).hexdigest() != entry['sha256']:
                raise ValueError('source SHA-256 mismatch')
            with Image.open(io.BytesIO(raw)) as opened:
                if opened.format != 'PNG' or opened.mode != 'RGBA':
                    raise ValueError('frame must be PNG RGBA')
                source = opened.copy()
            alpha = validate_alpha(source, name)
            if source_size is None:
                source_size = source.size
            if source.size != source_size:
                raise ValueError('all poses must share authored canvas dimensions')
            pixel_digest = hashlib.sha256(source.tobytes()).hexdigest()
            if pixel_digest in hashes:
                raise ValueError('duplicate decoded source poses; meaningful distinct poses required')
            hashes.add(pixel_digest)
            sources.append(source)
            provenance.append({'pose': name, 'source': entry['source'], 'sourceSha256': entry['sha256'], 'sourceAlpha': alpha})
        height = round(source_size[1] * width / source_size[0])
        if height < 1 or origin[0] + width > fw or origin[1] + height > fh:
            raise ValueError('shared transform would clip authored canvas')
        exports = [(asset, 1)]
        if hd:
            if (hd['width'], hd['height'], hd['pixelDensity']) != (asset['width'] * 2, fh * 2, 2):
                raise ValueError('unsupported HD contract')
            exports.append((hd, 2))
        for record, density in exports:
            target = within(pack, record['png'])
            if target.parent != (pack / ('runtime-hd' if density == 2 else 'runtime') / category).resolve() or target.suffix != '.png' or target in targets:
                raise ValueError('unexpected or duplicate destination')
            targets.add(target)
            sheet = Image.new('RGBA', (fw * len(order) * density, fh * density))
            frame_reports, rendered_hashes = [], set()
            for i, (name, source) in enumerate(zip(order, sources)):
                resized = source.resize((width * density, height * density), Image.Resampling.LANCZOS)
                cell = Image.new('RGBA', (fw * density, fh * density))
                cell.paste(resized, (origin[0] * density, origin[1] * density))
                alpha = validate_alpha(cell, name)
                digest = hashlib.sha256(cell.tobytes()).hexdigest()
                if digest in rendered_hashes:
                    raise ValueError('duplicate rendered poses at target resolution')
                rendered_hashes.add(digest)
                sheet.paste(cell, (i * fw * density, 0))
                frame_reports.append({'pose': name, 'pixelSha256': digest, 'alpha': alpha})
            report = {'topicId': topic, 'density': density, 'output': str(target.relative_to(root)),
                      'size': list(sheet.size), 'logicalAnchor': asset['anchor'], 'placement': placement,
                      'renderedCanvasSize': [width * density, height * density], 'sources': provenance,
                      'frames': frame_reports, 'pixelSha256': hashlib.sha256(sheet.tobytes()).hexdigest(),
                      'semanticAnimationReview': 'required-separately'}
            planned.append((target, sheet, report))
    return planned


def export(root, catalog, check=False):
    planned = plan(root, catalog)
    for target, sheet, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != 'RGBA' or current.size != sheet.size or current.tobytes() != sheet.tobytes():
                    raise ValueError('stale frame export')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            sheet.save(target, format='PNG', optimize=True)
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))
