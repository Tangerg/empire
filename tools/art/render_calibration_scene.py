#!/usr/bin/env python3
"""Render extracted real BoardPieces with isolated, source-foot-registered art trials."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from PIL import Image
from calibration_ground import append_ground

NS = 'http://www.w3.org/2000/svg'
XLINK = 'http://www.w3.org/1999/xlink'
PROPORTION_SETS = {
    'S0': {'keep':128,'human':34,'pine':90,'road':15.36},
    'P1': {'keep':144,'human':28,'pine':76,'road':22},
    'H1': {'keep':160,'human':34,'pine':70,'road':30},
}
ET.register_namespace('', NS)
ET.register_namespace('xlink', XLINK)


def element(tag, attrs=None):
    return ET.Element(f'{{{NS}}}{tag}', attrs or {})


def validate_stage(stage):
    units = stage['units']
    if not stage.get('stateUnchanged') or len(units) != 6 or not all(unit['canReceiveFoot'] for unit in units):
        raise ValueError('staged fixture must have six validated cells and untouched actual state')
    if len({(unit['x'],unit['y']) for unit in units}) != 6 or sorted(unit['owner'] for unit in units) != [1,1,1,2,2,2]:
        raise ValueError('staged fixture must have distinct cells and exactly three actors per team')


def build(root, source, masters, output, connectors=None, only=None, water_area=None, bank=None, ground_base=None, grass_patch=None, ground_period=128, ground_flat=False, grass_broad=False, proportion_set=None, tight_camera=False, staged_fixture=None):
    data = json.loads((source / 'pieces.json').read_text())
    stage = json.loads(staged_fixture.read_text()) if staged_fixture else None
    if stage: validate_stage(stage)
    output.mkdir(parents=True, exist_ok=True)
    reports = []
    for name, keep_width, human_height in [('renderer-baseline', None, 44), ('trial-keep96', 96, 44), ('trial-keep144', 144, 44), ('trial-keep144-human34', 144, 34), ('selected-layer-keep128-human34', 128, 34), ('selected-layer-corrected-connectors', 128, 34), ('selected-layer-area-water', 128, 34), ('selected-layer-area-water-bridge', 128, 34), ('selected-layer-area-water-bridge-bank', 128, 34)]:
        if only and name != only: continue
        selected = name.startswith('selected-layer')
        tree_height = 90
        if proportion_set:
            preset = PROPORTION_SETS[proportion_set]
            keep_width, human_height, tree_height = preset['keep'], preset['human'], preset['pine']
        camera = stage['camera'] if stage else ([-24,0,560,448] if tight_camera else [-96,0,640,512])
        native_size = (448,280) if stage else (640,512)
        svg = element('svg', {'viewBox': ' '.join(map(str,camera)), 'width': str(native_size[0]*2), 'height': str(native_size[1]*2)})
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

        def master_piece(identity, point, foot, scale, source_file=None):
            path = source_file or masters / identity / 'master.png'
            with Image.open(path) as image:
                width, height = image.size
                visible = image.convert('RGBA').getchannel('A').point(lambda value: 255 if value >= 128 else 0).getbbox()
            image = element('image', {'href': path.as_uri(), 'x': str(point[0] - foot[0] * scale),
                'y': str(point[1] - foot[1] * scale), 'width': str(width * scale), 'height': str(height * scale)})
            svg.append(image)
            substitutions.append({'identity': identity, 'source': str(path), 'pointWorld': point,
                'sourceFoot': foot, 'logicalPerSourcePixel': scale, 'fullCanvasLogical': [width * scale, height * scale],
                'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                'alpha128SourceBounds': list(visible), 'visibleLogicalSize': [(visible[2]-visible[0])*scale,(visible[3]-visible[1])*scale],
                'visibleWorldBounds': [point[0]+(visible[0]-foot[0])*scale,point[1]+(visible[1]-foot[1])*scale,point[0]+(visible[2]-foot[0])*scale,point[1]+(visible[3]-foot[1])*scale]})

        ground_report = append_ground(svg, defs, data['map'], ground_base, grass_patch, ground_period, ground_flat, grass_broad) if ground_base else None
        water_inserted = False
        for piece in data['layers']['ground']:
            if ground_base and 'surface-meadow@2x.png' in piece['markup']:
                continue
            if selected and 'candidate-environment-prop' in piece['markup']:
                continue
            if selected and 'transition-meadow-forest@2x.png' in piece['markup']:
                continue
            if selected and ('route-dirt-road@2x.png' in piece['markup'] or 'route-edge-dirt-road@2x.png' in piece['markup']):
                cell_x, cell_y = round(piece['x'] / 32), round(piece['y'] / 32)
                terrain = data['map']['tiles'][cell_y * data['map']['width'] + cell_x]
                if terrain != 'road': continue
            if water_area and 'area-water' in name and 'water-slow-river@2x.png' in piece['markup']:
                if not water_inserted:
                    with Image.open(water_area) as image: area_width, area_height = image.size
                    svg.append(element('image', {'href': water_area.as_uri(), 'x': '0', 'y': '0', 'width': str(area_width / 2), 'height': str(area_height / 2)}))
                    water_inserted = True
                continue
            add_piece(piece, 'ground')
        if bank and name.endswith('-bank'):
            master_piece('riverbank-module-v2', [320, 414], [660, 1475], 80 / 1453, bank)
        for index, piece in enumerate(data['layers']['underUnits']):
            if selected:
                if index in (11, 19, 27, 29, 36):
                    master_piece('pine', [piece['x'], piece['y']], [640, 1201], tree_height / 1152)
                elif 'crossings-fortifications' in piece['markup']:
                    if '-bridge' in name:
                        master_piece('stone-bridge', [piece['x'], piece['y']], [627, 625], 96 / 1186)
                    else:
                        add_piece(piece, 'legacy-crossing')
                continue
            if keep_width and index == 19:
                if abs(piece['x'] - 80.18315) > .01 or abs(piece['y'] - 197.76) > .01:
                    raise ValueError('selected actual forest placement changed')
                master_piece('pine', [piece['x'], piece['y']], [640, 1201], tree_height / 1152)
            else: add_piece(piece, 'scenery')
        for piece in data['buildings']:
            if selected and (piece['x'], piece['y']) != (32, 96): continue
            if keep_width and (piece['x'], piece['y']) == (32, 96):
                master_piece('lorne-keep', [48, 127], [632, 1177], keep_width / 1217)
            else: add_piece(piece, 'building')
        displayed_units = [{'x':u['x']*32,'y':u['y']*32,'owner':u['owner']} for u in stage['units']] if stage else data['units']
        for index, piece in enumerate(displayed_units):
            if selected:
                if stage or index < 5:
                    if stage:
                        marker = stage['markerContract']; contact = marker['contact']; ring = marker['ring']
                        svg.append(element('ellipse', {'cx':str(piece['x']+contact['cx']), 'cy':str(piece['y']+contact['cy']), 'rx':str(contact['rx']), 'ry':str(contact['ry']), 'fill':contact['fill'], 'opacity':str(contact['opacity'])}))
                        svg.append(element('ellipse', {'cx':str(piece['x']+ring['cx']), 'cy':str(piece['y']+ring['cy']), 'rx':str(ring['rx']), 'ry':str(ring['ry']), 'fill':ring['fill'], 'stroke':marker['colors'][str(piece['owner'])], 'stroke-width':str(ring['strokeWidth'])}))
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
            if connectors and ('corrected-connectors' in name or 'area-water' in name) and path.name in ('route-dirt-road@2x.png', 'route-edge-dirt-road@2x.png', 'water-slow-river@2x.png'):
                path = connectors / path.name
            if not path.is_file(): raise ValueError(f'missing image {path}')
            image.set('href', path.as_uri()); image.set(f'{{{XLINK}}}href', path.as_uri())
            hashes[str(path)] = hashlib.sha256(path.read_bytes()).hexdigest()
        svg_path = output / f'{name}.svg'
        ET.ElementTree(svg).write(svg_path, encoding='unicode')
        png = output / f'{name}-2x.png'
        subprocess.run(['inkscape', str(svg_path), '--export-type=png', f'--export-filename={png}'], check=True, capture_output=True)
        with Image.open(png) as image:
            image.resize(native_size, Image.Resampling.LANCZOS).save(output / f'{name}-native.png')
        reports.append({'variant': name, 'stagedFixture': stage, 'viewBox': camera, 'proportionStudy': {'set': proportion_set, 'humanVisibleHeight': human_height, 'pineVisibleHeight': tree_height, 'keepVisibleWidth': keep_width, 'bridgeVisibleWidth': 96, 'roadVisualWidth': PROPORTION_SETS.get(proportion_set, PROPORTION_SETS['S0'])['road'], 'roadWidthRequest': 32 if proportion_set == 'H1' else PROPORTION_SETS.get(proportion_set, PROPORTION_SETS['S0'])['road'], 'positionsUnchanged': not bool(stage), 'tightCamera': tight_camera}, 'cropWorld': [value/32 for value in camera] if stage else [0, 3, 14, 10],
            'overscanLogical': 0 if stage else 96, 'groundStudy': ground_report, 'groundFit': {'meadowInset': 0 if keep_width else 3, 'bleed': .5},
            'substitutions': substitutions, 'inputHashes': hashes, 'kind': 'actual-renderer-piece-art-study-not-gameplay',
            'connectorCalibration': str(connectors) if connectors and ('corrected-connectors' in name or 'area-water' in name) else None,
            'waterAreaCalibration': str(water_area) if water_area and 'area-water' in name else None,
            'selectionStudy': {'enabled': selected, 'unitPositions': ('six validated staged cells, deliberately not initial deployment; shared swordsman identity with team rings' if stage else 'first five actual party deployments; all deliberately represented by one swordsman identity') if selected else 'actual original identities except selected Torren', 'forestPositions': 'five actual canopy placement points represented by the same pine' if selected else 'one pine substitution', 'omitted': 'all legacy ground decals, foundation props, forest-floor blend underlays and settlement road-threshold underlays; other standing scenery, other buildings, remaining units and foreground; tactical terrain surfaces/transitions/routes/water and data unchanged' if selected else 'none beyond stated effects'},
            'provenance': 'Only listed master substitutions and explicitly named connector/water layers use new calibration; other pixels remain current branch mixed legacy/previous remake',
            'limitations': (['STAGED six-unit encounter, not original deployment or observed gameplay', 'Shared swordsman identity and same facing for both teams', 'Runtime contact ellipse and team rings restored; no dynamic lighting, animation or combat UI', 'Selected scenery omissions remain explicit'] if stage else data['presentationSubstitutions']), 'acceptance': 'pending-user-style-review'})
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
    parser.add_argument('--water-area', type=Path)
    parser.add_argument('--bank', type=Path)
    parser.add_argument('--ground-base', type=Path)
    parser.add_argument('--grass-patch', type=Path)
    parser.add_argument('--staged-fixture', type=Path)
    parser.add_argument('--proportion-set', choices=('S0','P1','H1'))
    parser.add_argument('--tight-camera', action='store_true')
    parser.add_argument('--ground-flat', action='store_true')
    parser.add_argument('--grass-broad', action='store_true')
    parser.add_argument('--ground-period', type=int, choices=(128,512), default=128)
    args = parser.parse_args()
    build(args.root.resolve(), args.source.resolve(), args.masters.resolve(), args.output.resolve(), args.connectors.resolve() if args.connectors else None, args.only, args.water_area.resolve() if args.water_area else None, args.bank.resolve() if args.bank else None, args.ground_base.resolve() if args.ground_base else None, args.grass_patch.resolve() if args.grass_patch else None, args.ground_period, args.ground_flat, args.grass_broad, args.proportion_set, args.tight_camera, args.staged_fixture.resolve() if args.staged_fixture else None)
