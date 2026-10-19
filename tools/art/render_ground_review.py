"""Rasterize runtime ground evidence at native 1x/2x and verify crop invariance."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from PIL import Image, ImageChops
from svg_viewports import NS, flatten_viewports


def render(directory):
    names = ('field-full', 'integration-full', 'field-seam-crop', 'integration-seam-crop',
             'field-period-1024-diagnostic', 'integration-period-1024-diagnostic',
             'field-period-1024-crop-diagnostic', 'integration-period-1024-crop-diagnostic')
    with tempfile.TemporaryDirectory() as profile:
        for name in names:
            source = directory / f'{name}.svg'
            svg = ET.parse(source).getroot()
            # Keep the original runtime SVG; normalize only the offline copy.
            defs = ET.SubElement(svg, f'{{{NS}}}defs')
            flatten_viewports(svg, defs)
            normalized = directory / f'{name}.offline.svg'
            ET.ElementTree(svg).write(normalized, encoding='unicode')
            for density in (1, 2):
                subprocess.run(['inkscape', str(normalized), '--export-type=png',
                                f'--export-width={int(float(svg.get("width"))) * density}',
                                f'--export-filename={directory / f"{name}@{density}x.png"}'],
                               env={**os.environ, 'INKSCAPE_PROFILE_DIR': profile}, check=True, capture_output=True)
            normalized.unlink()
    crops = []
    for kind in ('field', 'integration'):
        for density in (1, 2):
            with Image.open(directory / f'{kind}-full@{density}x.png') as full, Image.open(directory / f'{kind}-seam-crop@{density}x.png') as cropped:
                expected = full.crop(tuple(n * density for n in (448, 64, 704, 320))).convert('RGBA')
                difference = ImageChops.difference(expected, cropped.convert('RGBA'))
                # Compare color channels as well: RGBA getbbox alone may ignore
                # color differences when the alpha difference is identically zero.
                maximum = max(channel.getextrema()[1] for channel in difference.split())
                if kind == 'field' and maximum:
                    raise ValueError(f'world crop changed {kind} at {density}x: max channel difference {maximum}')
                crops.append({'kind': kind, 'density': density, 'exactPixelMatch': maximum == 0,
                              'maximumChannelDifference': maximum,
                              'note': 'World material requires exact pixels; integration also contains independently antialiased legacy atlas/prop clips'})
    result = {'evidence': 'offline Inkscape native rendering of actual BoardPieces', 'cropChecks': crops,
              'fullLogicalExtent': [704, 448], 'browserPlayback': False,
              'repeatSeamVisualReview': 'required separately; these tests prove phase, not artistic tiling'}
    (directory / 'raster-validation.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    print(json.dumps(render(args.directory.resolve()), indent=2))
