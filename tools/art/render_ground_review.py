"""Rasterize runtime evidence at display 1x/2x, declaring actual source density."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from PIL import Image, ImageChops
from svg_viewports import NS, flatten_viewports


def compare_retained(directory, baseline):
    reports = []
    for kind in ('field', 'integration'):
        for density in (1, 2):
            pairs = (
                (f'{kind}-full@{density}x.png', f'{kind}-period-1024-diagnostic@{density}x.png'),
                (f'{kind}-high-density-alternative@{density}x.png', f'{kind}-full@{density}x.png'),
            )
            for current_name, previous_name in pairs:
                with Image.open(directory / current_name) as current, Image.open(baseline / previous_name) as previous:
                    if current.size != previous.size:
                        raise ValueError(f'sampling promotion changed comparison dimensions: {current_name}')
                    current = current.convert('RGBA')
                    difference = ImageChops.difference(current, previous.convert('RGBA'))
                    maximum = max(channel.getextrema()[1] for channel in difference.split())
                    if maximum:
                        raise ValueError(f'sampling promotion changed comparison pixels: {current_name}')
                    reports.append({'runtimeRender': current_name,
                                    'storedComparison': os.path.relpath(baseline / previous_name, directory),
                                    'maximumChannelDifference': maximum, 'exactPixelMatch': True,
                                    'runtimePixelSha256': hashlib.sha256(current.tobytes()).hexdigest()})
    result = {'kind': 'Actual runtime after metadata promotion; comparison with retained first-commit samples', 'comparisons': reports}
    (directory / 'promotion-comparison.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


def validate_water_render(directory, provenance):
    from calibrate_water_area import render_water_area
    root = Path(__file__).resolve().parents[2]
    source = root / 'packages/story-candidate-01/assets/final-fantasy-v1' / provenance['material']['source']
    expected, _ = render_water_area(source, provenance['waterAndBridgeCells'], 64, provenance['mapSize'],
                                    uv_cells=provenance['material']['logicalSize'][0] // 32)
    with Image.open(directory / 'field-full@2x.png') as image:
        actual = image.convert('RGBA')
    if actual.size != expected.size:
        raise ValueError('runtime water extent differs from the actual map')
    difference = ImageChops.difference(actual, expected)
    if difference.getchannel('A').getextrema()[1]:
        raise ValueError('runtime water alpha differs from the approved occupancy geometry')
    opaque = expected.getchannel('A').point(lambda value: 255 if value == 255 else 0)
    if any(ImageChops.multiply(channel, opaque).getextrema()[1] for channel in difference.split()[:3]):
        raise ValueError('runtime water RGB differs from world-coordinate material sampling')
    return {'native2xAlphaExact': True, 'native2xOpaqueRGBExact': True,
            'outsideOccupiedCellsAlpha': 0, 'waterAndBridgeCellCount': len(provenance['waterAndBridgeCells']),
            'comparison': 'Actual SVG raster against approved occupancy/UV calibration; transparent RGB ignored'}


def rasterize(directory, densities=(1, 2)):
    names = sorted(path.stem for path in directory.glob('*.svg') if not path.name.endswith('.offline.svg'))
    with tempfile.TemporaryDirectory() as profile:
        for name in names:
            source = directory / f'{name}.svg'
            svg = ET.parse(source).getroot()
            # Keep the original runtime SVG; normalize only the offline copy.
            normalize_actor_facing(svg)
            defs = ET.SubElement(svg, f'{{{NS}}}defs')
            flatten_viewports(svg, defs)
            normalized = directory / f'{name}.offline.svg'
            ET.ElementTree(svg).write(normalized, encoding='unicode')
            for density in densities:
                subprocess.run(['inkscape', str(normalized), '--export-type=png',
                                f'--export-width={int(float(svg.get("width"))) * density}',
                                f'--export-filename={directory / f"{name}@{density}x.png"}'],
                               env={**os.environ, 'INKSCAPE_PROFILE_DIR': profile}, check=True, capture_output=True)
            normalized.unlink()


def normalize_actor_facing(svg):
    """Spell the app's CSS mirror as geometry for offline renderers."""
    for style in svg.iter(f'{{{NS}}}style'):
        if style.text:
            style.text = re.sub(r'\.layer-units \.unit\.face-left > \.figure\s*\{[^}]*\}', '', style.text)
    for unit in svg.iter(f'{{{NS}}}g'):
        if {'unit', 'face-left'} <= set(unit.get('class', '').split()):
            for child in unit:
                if child.get('class') == 'figure':
                    child.set('transform', 'translate(32 0) scale(-1 1)')


def render(directory):
    rasterize(directory)
    provenance = json.loads((directory / 'runtime-provenance.json').read_text())
    x, y, w, h = provenance['cropWorldBox']
    crops = []
    for kind in ('field', 'integration'):
        for density in (1, 2):
            with Image.open(directory / f'{kind}-full@{density}x.png') as full, Image.open(directory / f'{kind}-seam-crop@{density}x.png') as cropped:
                expected = full.crop(tuple(n * density for n in (x, y, x + w, y + h))).convert('RGBA')
                difference = ImageChops.difference(expected, cropped.convert('RGBA'))
                # Compare color channels as well: RGBA getbbox alone may ignore
                # color differences when the alpha difference is identically zero.
                maximum = max(channel.getextrema()[1] for channel in difference.split())
                crop_detail = {}
                if kind == 'field':
                    if 'edgeDecoration' in provenance:
                        from review_edge_decoration import compare_edge_crop
                        crop_detail = compare_edge_crop(expected, cropped.convert('RGBA'))
                    elif maximum:
                        raise ValueError(f'world crop changed {kind} at {density}x: max channel difference {maximum}')
                crops.append({'kind': kind, 'density': density, 'exactPixelMatch': maximum == 0,
                              'maximumChannelDifference': maximum,
                              'note': 'World material requires exact pixels; integration also contains independently antialiased legacy atlas/prop clips',
                              **crop_detail})
    provenance = json.loads((directory / 'runtime-provenance.json').read_text())
    result = {'selectedLogicalPeriod': provenance['material']['logicalSize'],
              'sourcePixelsPerWorldUnit': provenance['material']['rasterPixelsPerWorldUnit'],
              'evidence': 'offline Inkscape display-1x/2x rendering of actual BoardPieces', 'cropChecks': crops,
              'fullLogicalExtent': [704, 448], 'browserPlayback': False,
              'repeatSeamVisualReview': 'required separately; these tests prove phase, not artistic tiling'}
    if provenance['material']['id'] == 'water-area':
        result['waterCalibrationComparison'] = validate_water_render(directory, provenance)
    if 'edgeDecoration' in provenance:
        from review_edge_decoration import validate_edges
        result = {'evidence': result['evidence'], 'cropChecks': crops, 'fullLogicalExtent': [704, 448],
                  'browserPlayback': False, 'edgeSafety': validate_edges(directory, provenance),
                  'sampling': 'Whole authored overlays at fixed world coordinates; no cell reset or repeating texture'}
    (directory / 'raster-validation.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--compare-directory', type=Path)
    parser.add_argument('--scene-only', action='store_true', help='Rasterize complete runtime scene SVGs without ground crop checks')
    args = parser.parse_args()
    if args.scene_only:
        rasterize(args.directory.resolve())
        print(json.dumps({'kind': 'offline runtime scene rasterization', 'browserPlayback': False}))
    else:
        print(json.dumps(render(args.directory.resolve()), indent=2))
    if args.compare_directory:
        print(json.dumps(compare_retained(args.directory.resolve(), args.compare_directory.resolve()), indent=2))
