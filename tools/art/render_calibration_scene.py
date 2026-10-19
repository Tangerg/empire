#!/usr/bin/env python3
"""Render extracted real BoardPieces with isolated, source-foot-registered art trials."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from PIL import Image

NS = 'http://www.w3.org/2000/svg'
XLINK = 'http://www.w3.org/1999/xlink'
ET.register_namespace('', NS)
ET.register_namespace('xlink', XLINK)


def element(tag, attrs=None):
    return ET.Element(f'{{{NS}}}{tag}', attrs or {})


def build(root, source, masters, output, connectors=None, only=None):
    data = json.loads((source / 'pieces.json').read_text())
    output.mkdir(parents=True, exist_ok=True)
    reports = []
    for name, keep_width, human_height in [('renderer-baseline', None, 44), ('trial-keep96', 96, 44), ('trial-keep144', 144, 44), ('trial-keep144-human34', 144, 34), ('selected-layer-keep128-human34', 128, 34), ('selected-layer-corrected-connectors', 128, 34)]:
        if only and name != only: continue
        selected = name.startswith('selected-layer')
        svg = element('svg', {'viewBox': '-96 0 640 512', 'width': '1280', 'height': '1024'})
        defs = element('defs'); svg.append(defs)
        hashes = {}; substitutions = []; clip_index = 0

        def add_piece(piece, layer):
            nonlocal clip_index
            group = element('g', {'transform': f"translate({piece['x']:.2f},{piece['y']:.2f})"})
            parsed = ET.fromstring(f'<svg xmlns="{NS}">{piece["markup"]}</svg>')
            if keep_width and layer == 'ground' and 'surface-meadow@2x.png' in piece['markup']:
                # Change only the explicit meadow sampling trial; retain its SVG bleed.
                for image in parsed.iter(f'{{{NS}}}image'):
                    for key in ('width', 'height'):
                        image.set(key, str(float(image.get(key)) / (32 / 26)))
                    for key in ('x', 'y'):
                        image.set(key, str(float(image.get(key)) / (32 / 26) + 3))
            group.extend(list(parsed)); svg.append(group)

        def master_piece(identity, point, foot, scale):
            path = masters / identity / 'master.png'
            with Image.open(path) as image:
                width, height = image.size
            image = element('image', {'href': path.as_uri(), 'x': str(point[0] - foot[0] * scale),
                'y': str(point[1] - foot[1] * scale), 'width': str(width * scale), 'height': str(height * scale)})
            svg.append(image)
            substitutions.append({'identity': identity, 'source': str(path), 'pointWorld': point,
                'sourceFoot': foot, 'logicalPerSourcePixel': scale, 'fullCanvasLogical': [width * scale, height * scale],
                'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest()})

        for piece in data['layers']['ground']: add_piece(piece, 'ground')
        for index, piece in enumerate(data['layers']['underUnits']):
            if selected:
                if index in (11, 19, 27, 29, 36):
                    master_piece('pine', [piece['x'], piece['y']], [640, 1201], 90 / 1152)
                elif 'crossings-fortifications' in piece['markup']:
                    add_piece(piece, 'legacy-crossing')
                continue
            if keep_width and index == 19:
                if abs(piece['x'] - 80.18315) > .01 or abs(piece['y'] - 197.76) > .01:
                    raise ValueError('selected actual forest placement changed')
                master_piece('pine', [piece['x'], piece['y']], [640, 1201], 90 / 1152)
            else: add_piece(piece, 'scenery')
        for piece in data['buildings']:
            if selected and (piece['x'], piece['y']) != (32, 96): continue
            if keep_width and (piece['x'], piece['y']) == (32, 96):
                master_piece('lorne-keep', [48, 127], [632, 1177], keep_width / 1217)
            else: add_piece(piece, 'building')
        for index, piece in enumerate(data['units']):
            if selected:
                if index < 5:
                    master_piece('swordsman', [piece['x'] + 16, piece['y'] + 31], [641, 965], human_height / 632)
                continue
            if keep_width and (piece['x'], piece['y']) == (64, 256):
                master_piece('swordsman', [80, 287], [641, 965], human_height / 632)
            else: add_piece(piece, 'unit')
        if not selected:
            for piece in data['layers']['overUnits']: add_piece(piece, 'foreground')

        # Inkscape does not reliably apply nested SVG overflow clipping. Spell
        # the identical viewport clipping and viewBox transform explicitly.
        for parent in list(svg.iter()):
            for child in list(parent):
                if child.tag != f'{{{NS}}}svg': continue
                clip_index += 1
                vx, vy, vw, vh = map(float, child.get('viewBox').split())
                x, y = float(child.get('x', '0')), float(child.get('y', '0'))
                w, h = float(child.get('width', str(vw))), float(child.get('height', str(vh)))
                clip = ET.SubElement(defs, f'{{{NS}}}clipPath', {'id': f'viewport-{clip_index}', 'clipPathUnits': 'userSpaceOnUse'})
                clip.append(element('rect', dict(zip(('x', 'y', 'width', 'height'), map(str, (vx, vy, vw, vh))))))
                group = element('g', {'transform': f'translate({x} {y}) scale({w/vw} {h/vh}) translate({-vx} {-vy})'})
                inside = ET.SubElement(group, f'{{{NS}}}g', {'clip-path': f'url(#viewport-{clip_index})'})
                inside.extend(list(child)); parent.insert(list(parent).index(child), group); parent.remove(child)
        for image in svg.iter(f'{{{NS}}}image'):
            href = image.get('href')
            path = root / href.lstrip('/') if href.startswith('/packages/') else Path(href.removeprefix('file://'))
            if connectors and name.endswith('corrected-connectors') and path.name in ('route-dirt-road@2x.png', 'route-edge-dirt-road@2x.png', 'water-slow-river@2x.png'):
                path = connectors / path.name
            if not path.is_file(): raise ValueError(f'missing image {path}')
            image.set('href', path.as_uri()); image.set(f'{{{XLINK}}}href', path.as_uri())
            hashes[str(path)] = hashlib.sha256(path.read_bytes()).hexdigest()
        svg_path = output / f'{name}.svg'
        ET.ElementTree(svg).write(svg_path, encoding='unicode')
        png = output / f'{name}-2x.png'
        subprocess.run(['inkscape', str(svg_path), '--export-type=png', f'--export-filename={png}'], check=True, capture_output=True)
        with Image.open(png) as image:
            image.resize((640, 512), Image.Resampling.LANCZOS).save(output / f'{name}-native.png')
        reports.append({'variant': name, 'viewBox': [-96, 0, 640, 512], 'cropWorld': [0, 3, 14, 10],
            'overscanLogical': 96, 'groundFit': {'meadowInset': 0 if keep_width else 3, 'bleed': .5},
            'substitutions': substitutions, 'inputHashes': hashes, 'kind': 'actual-renderer-piece-art-study-not-gameplay',
            'connectorCalibration': str(connectors) if connectors and name.endswith('corrected-connectors') else None,
            'selectionStudy': {'enabled': selected, 'unitPositions': 'first five actual party deployments; all deliberately represented by one swordsman identity' if selected else 'actual original identities except selected Torren', 'forestPositions': 'five actual canopy placement points represented by the same pine' if selected else 'one pine substitution', 'omitted': 'other standing scenery, other buildings, remaining units and foreground; tactical data unchanged' if selected else 'none beyond stated effects'},
            'provenance': 'Only three calibration masters are new style; other pixels are current branch mixed legacy/previous remake',
            'limitations': data['presentationSubstitutions'], 'acceptance': 'pending-user-style-review'})
    (output / 'registration-and-provenance.json').write_text(json.dumps(reports, indent=2) + '\n')
    return reports


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--masters', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--connectors', type=Path)
    parser.add_argument('--only')
    args = parser.parse_args()
    build(args.root.resolve(), args.source.resolve(), args.masters.resolve(), args.output.resolve(), args.connectors.resolve() if args.connectors else None, args.only)
