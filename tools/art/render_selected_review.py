"""Render selected-actor review at actual 1x display size, then compare whole scenes."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw
from render_ground_review import rasterize


def render(directory):
    rasterize(directory, densities=(1,))
    provenance = json.loads((directory / 'selection-provenance.json').read_text())
    scenarios = ['tree', 'granary', 'unobstructed', 'hidden-selected']
    sheet = Image.new('RGB', (640, 920), '#1b211d')
    draw = ImageDraw.Draw(sheet)
    comparisons = []
    for row, name in enumerate(scenarios):
        record = next(item for item in provenance['renders'] if item['scenario'] == name)
        x, y = record['screenFoot']
        box = (x - 70, y - 90, x + 90, y + 15)
        label = f"{name}: existing selection (left) / {provenance['opacity']:.0%} cue (right)"
        draw.text((8, row * 230 + 6), label, fill='white')
        images = []
        for column, mode in enumerate(('baseline', 'cue')):
            image = Image.open(directory / f'{name}-{mode}@1x.png').convert('RGBA')
            images.append(image)
            crop = image.crop(box).resize((320, 210), Image.Resampling.NEAREST)
            sheet.paste(crop, (column * 320, row * 230 + 24))
        difference = ImageChops.difference(*images)
        maximum = max(channel.getextrema()[1] for channel in difference.split())
        if name == 'hidden-selected' and maximum:
            raise ValueError('hidden selected actor changed scene pixels')
        if name != 'hidden-selected' and not maximum:
            raise ValueError(f'visible selected cue has no rendered pixels: {name}')
        comparisons.append({'scenario': name, 'cropScreenBox': box, 'maximumChannelDifference': maximum,
                            'differenceBounds': difference.convert('RGB').getbbox(),
                            'baselinePixelSha256': hashlib.sha256(images[0].tobytes()).hexdigest(),
                            'cuePixelSha256': hashlib.sha256(images[1].tobytes()).hexdigest(),
                            'exactPixelMatch': maximum == 0})
    sheet.save(directory / 'selection-comparison-detail.png')
    report = {'kind': 'Offline actual 1x scene comparison; contact-sheet details enlarged 2x with nearest-neighbor pixels',
              'opacity': provenance['opacity'], 'comparisons': comparisons, 'browserPlayback': False}
    (directory / 'selection-raster-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    render(parser.parse_args().directory.resolve())
