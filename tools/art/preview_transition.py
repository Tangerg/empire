#!/usr/bin/env python3
"""Review real meadow/forest transition pixels without rebuilding their masks."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, read_json
from export_environment_overrides import PACK

NEIGHBORS = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]


def island_mask(x, y):
    return sum(1 << bit for bit, (dx, dy) in enumerate(NEIGHBORS) if 1 <= x + dx <= 3 and 1 <= y + dy <= 3)


def build(root, output):
    env = root / PACK / 'environment-builder-v1'
    manifest = read_json(env / 'manifest-environment-builder-v1.json')
    atlases = {a['id']: a for a in manifest['atlases']}
    base = atlases['surface-meadow']
    overlay = atlases['transition-meadow-forest']
    if overlay['bitOrder'] != ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'] or overlay['cellWidth2x'] != 64 or overlay['tileMode'] != 'blob-47':
        raise ValueError('unexpected transition contract')
    paths = [env / a['png2x'] for a in (base, overlay)]
    background = Image.open(paths[0]).convert('RGBA').crop((0, 0, 64, 64))
    sheet = Image.open(paths[1]).convert('RGBA')
    canvas = Image.new('RGBA', (320, 320))
    placements = []
    for y in range(5):
        for x in range(5):
            canvas.alpha_composite(background, (x * 64, y * 64))
    for y in range(1, 4):
        for x in range(1, 4):
            mask = island_mask(x, y)
            index = overlay['maskOrder'].index(mask)
            sx, sy = index % overlay['columns'] * 64, index // overlay['columns'] * 64
            cell = sheet.crop((sx, sy, sx + 64, sy + 64))
            canvas.alpha_composite(cell, (x * 64, y * 64))
            placements.append({'tile': [x, y], 'mask': mask, 'index': index})
    output.mkdir(parents=True, exist_ok=True)
    canvas.save(output / 'transition-compatibility-2x.png')
    canvas.resize((160, 160), Image.Resampling.LANCZOS).save(output / 'transition-compatibility-native.png')
    report = {'kind': 'actual-pixel-transition-art-review-not-gameplay', 'logicalTileSize': 32,
              'baseCell': 'meadow-1', 'overlay': overlay['id'], 'placements': placements,
              'inputsSha256': {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
              'maskAndManifestMutation': False, 'runtimeEffects': 'none; no camera or sunlight',
              'acceptance': 'requires visual palette and boundary review'}
    (output / 'transition-placement.json').write_text(json.dumps(report, indent=2) + '\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(build(args.root.resolve(), args.output_dir.resolve()), indent=2))
