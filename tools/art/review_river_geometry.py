"""Check actual rendered river alpha against tactical cells; offline geometry, not runtime QA.

Usage: python tools/art/review_river_geometry.py FULL_BOARD_SVG LEVEL_JSON OUTPUT
The full uncropped scene remains the visual proof. These isolated masks only
measure water permission, bank overhang and north-to-south channel continuity.
"""
from copy import deepcopy
from pathlib import Path
import argparse
import json
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image, ImageFilter
from render_ground_review import rasterize
from svg_viewports import NS

N = '{' + NS + '}'

def review(source, level, output):
    output.mkdir(parents=True, exist_ok=True)
    width, height = level['width'] * 32, level['height'] * 32
    ground = next(node for node in source.iter() if node.get('class') == 'layer layer-ground')
    for kind in ['area-water', 'river-bank']:
        root = ET.Element(N + 'svg', {'width': str(width), 'height': str(height), 'viewBox': f'0 0 {width} {height}'})
        for piece in ground:
            if not any(node.get('data-runtime-raster') == kind for node in piece.iter()):
                continue
            clone = deepcopy(piece)
            for parent in clone.iter():
                for image in list(parent):
                    if image.tag != N + 'image':
                        continue
                    image.tag = N + 'rect'
                    image.attrib.pop('href', None)
                    image.attrib.pop('{http://www.w3.org/1999/xlink}href', None)
                    image.set('fill', 'white')
            root.append(clone)
        if not len(root):
            raise ValueError(f'actual scene has no {kind} pieces')
        ET.ElementTree(root).write(output / f'{kind}.svg', encoding='unicode')
    rasterize(output, (1, 2))
    allowed = np.array([[cell in '~=' for cell in row] for row in level['terrain']])
    permitted_bank = np.array([[cell in '~=.-' for cell in row] for row in level['terrain']])
    checks = []
    for scale in [1, 2]:
        water = np.array(Image.open(output / f'area-water@{scale}x.png').convert('RGBA'))[:, :, 3]
        bank = np.array(Image.open(output / f'river-bank@{scale}x.png').convert('RGBA'))[:, :, 3]
        permission = np.repeat(np.repeat(allowed, 32 * scale, axis=0), 32 * scale, axis=1)
        bank_permission = np.repeat(np.repeat(permitted_bank, 32 * scale, axis=0), 32 * scale, axis=1)
        dry = int(np.count_nonzero((water > 1) & ~permission))
        blocked = int(np.count_nonzero((bank > 1) & ~bank_permission))
        rim_limit = np.array(Image.fromarray(permission.astype('uint8') * 255).filter(ImageFilter.MaxFilter(10 * scale + 1))) > 0
        excessive_rim = int(np.count_nonzero((bank > 1) & ~rim_limit))
        # Every scanline must meet the main channel, and adjacent rows must overlap.
        lines = [np.flatnonzero(row > 127) for row in water]
        gaps = [y for y, row in enumerate(lines) if not len(row)]
        disconnected = [y for y in range(1, len(lines)) if not np.intersect1d(lines[y-1], lines[y]).size]
        if dry or blocked or excessive_rim or gaps or disconnected:
            raise ValueError(f'{scale}x river geometry failed: dry={dry}, blocked={blocked}, excessive_rim={excessive_rim}, gaps={gaps}, disconnected={disconnected}')
        checks.append({'scale': scale, 'dryWaterPixels': dry, 'blockedBankPixels': blocked, 'beyondFivePixelRim': excessive_rim,
                       'emptyScanlines': len(gaps), 'disconnectedScanlines': len(disconnected)})
    (output / 'geometry.json').write_text(json.dumps({'kind': 'offline actual rendered material alpha', 'checks': checks}, indent=2) + '\n')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('svg', type=Path)
    parser.add_argument('level', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    review(ET.parse(args.svg).getroot(), json.loads(args.level.read_text()), args.output)
