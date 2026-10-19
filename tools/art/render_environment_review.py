"""Rasterize actual renderer markup and measure its authored alpha silhouette."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from urllib.parse import unquote, urlparse
from PIL import Image
from export_environment_overrides import PACK, cell_box
from export_overrides import ROOT
from svg_viewports import NS, flatten_viewports


def render(directory):
    svg_path = directory / 'native-contact.svg'
    svg = ET.parse(svg_path).getroot()
    defs = ET.SubElement(svg, f'{{{NS}}}defs')
    flatten_viewports(svg, defs)
    for image in svg.iter(f'{{{NS}}}image'):
        href = image.get('href')
        path = Path(unquote(urlparse(href).path)) if href.startswith('file:') else (directory / href).resolve()
        if not path.is_file():
            raise ValueError(f'missing renderer image: {path}')
        image.set('href', os.path.relpath(path, directory))
    ET.register_namespace('', NS)
    ET.ElementTree(svg).write(svg_path, encoding='unicode')
    with tempfile.TemporaryDirectory() as profile:
        subprocess.run(['inkscape', str(svg_path), '--export-type=png',
                        f'--export-filename={directory / "native-contact.png"}'],
                       env={**os.environ, 'INKSCAPE_PROFILE_DIR': profile}, check=True, capture_output=True)

    report_path = directory / 'runtime-scale-report.json'
    report = json.loads(report_path.read_text())
    environment = ROOT / PACK / 'environment-builder-v1'
    manifest = json.loads((environment / 'manifest-environment-builder-v1.json').read_text())
    for asset in report['assets']:
        atlas = next(a for a in manifest['atlases'] if asset['id'] in a.get('cellOrder', []) )
        with Image.open(environment / atlas['png2x']) as image:
            index = atlas['cellOrder'].index(asset['id'])
            alpha = image.crop(cell_box(index, 192)).getchannel('A')
            bounds = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
            logical_bounds = [value / 2 for value in bounds]
            visible_height = logical_bounds[3] - logical_bounds[1]
            asset['logicalVisibleBoundsAlpha128'] = logical_bounds
            asset['nativeSampleVisibleHeight'] = visible_height * asset['sampleScale']
            asset['nativeVisibleHeightRange'] = [visible_height * scale for scale in asset['rendererScaleRange']]
            asset['nativeSampleOverhangBelowContact'] = (logical_bounds[3] - asset['anchor'][1]) * asset['sampleScale']
    report['offlineViewportNormalization'] = 'nested SVG viewports expressed as equivalent clip paths for Inkscape'
    report_path.write_text(json.dumps(report, indent=2) + '\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    render(args.directory.resolve())
