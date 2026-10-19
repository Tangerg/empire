"""Rasterize actual stone projection fixtures and require exact alpha/world-UV parity."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops

from render_ground_review import rasterize


def compare(directory: Path):
    reports = []
    for density in (1, 2):
        for baseline, actual in [('legacy-alpha', 'world-alpha'), ('reference-uv', 'world-uv')]:
            with Image.open(directory / f'{baseline}@{density}x.png') as old, Image.open(directory / f'{actual}@{density}x.png') as new:
                old, new = old.convert('RGBA'), new.convert('RGBA')
                if old.size != new.size:
                    raise ValueError('stone fixture dimensions changed')
                difference = ImageChops.difference(old, new)
                maximum = max(channel.getextrema()[1] for channel in difference.split())
                if maximum:
                    raise ValueError(f'stone projection differs: {actual} at {density}x, max channel difference {maximum}')
                reports.append({'density': density, 'baseline': baseline, 'runtime': actual,
                                'maximumChannelDifference': maximum, 'exactRgbaMatch': True,
                                'baselinePixelSha256': hashlib.sha256(old.tobytes()).hexdigest(),
                                'runtimePixelSha256': hashlib.sha256(new.tobytes()).hexdigest()})
    return reports


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    rasterize(args.directory)
    result = compare(args.directory)
    (args.directory / 'comparison.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2))
