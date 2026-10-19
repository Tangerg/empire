"""Compare real pilot captures, quantify changed-wall overlap, and assemble evidence."""
import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image, ImageDraw
from render_ground_review import rasterize
from svg_viewports import NS

N = '{' + NS + '}'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def wall_occlusion(directory, output):
    metadata = json.loads((directory / 'provenance.json').read_text())
    cells = {tuple(c) for c in json.loads((directory / 'actual-topology.json').read_text())['replaced']}
    actors = metadata['actorCells']
    panel = 128
    records = []
    for mode in ('before', 'partial'):
        source = ET.parse(directory / f'{mode}-full.svg').getroot()
        layer = next(g for g in source.iter(N + 'g') if g.get('class') == 'layer layer-units')
        drawings = list(layer)
        positions = []
        for drawing in drawings:
            values = re.findall(r'-?\d+(?:\.\d+)?', drawing.get('transform', ''))
            positions.append(tuple(map(float, values[:2])) if len(values) >= 2 else None)
        wall_indices = []
        for index, drawing in enumerate(drawings):
            if mode == 'partial':
                if any(g.get('data-wall-cell') for g in drawing.iter(N + 'g')):
                    wall_indices.append(index)
            elif any(positions[index] and abs(positions[index][0] - (x + .5) * 32) < .001
                     and abs(positions[index][1] - (y + 1.18) * 32) < .001 for x, y in cells):
                wall_indices.append(index)
        assert len(wall_indices) == 27
        for kind in (('actor', 'wall') if mode == 'before' else ('wall',)):
            atlas = ET.Element(N + 'svg', {'width': '512', 'height': '768', 'viewBox': '0 0 512 768'})
            for style in source.iter(N + 'style'):
                atlas.append(deepcopy(style))
            for row, actor in enumerate(actors):
                x, y = actor['cell']
                index = next(i for i, g in enumerate(drawings) if 'unit' in g.get('class', '').split()
                             and positions[i] == (x * 32, y * 32))
                viewport = ET.SubElement(atlas, N + 'svg', {'x': str(row % 4 * panel), 'y': str(row // 4 * panel),
                    'width': str(panel), 'height': str(panel), 'viewBox': f'{x * 32 - 48} {y * 32 - 80} {panel} {panel}', 'overflow': 'hidden'})
                if kind == 'actor':
                    picture = deepcopy(drawings[index])
                    for child in list(picture):
                        if child.get('class') != 'figure':
                            picture.remove(child)
                        else:
                            for part in list(child):
                                if part.get('class') != 'board-strip':
                                    child.remove(part)
                    viewport.append(picture)
                else:
                    for i in wall_indices:
                        if i > index:
                            viewport.append(deepcopy(drawings[i]))
            # Repeated wall pictures share their identical, self-contained defs.
            for image in atlas.iter(N + 'image'):
                image.set('href', str((directory / image.get('href')).resolve()))
            ET.ElementTree(atlas).write(output / f'occlusion-{mode}-{kind}.svg', encoding='unicode')
    rasterize(output, densities=(1,))
    images = {name: np.asarray(Image.open(output / f'occlusion-{name}@1x.png').convert('RGBA'))[:, :, 3] / 255
              for name in ('before-actor', 'before-wall', 'partial-wall')}
    for row, actor in enumerate(actors):
        x, y = row % 4 * panel, row // 4 * panel
        alpha = images['before-actor'][y:y + panel, x:x + panel]
        assert alpha.sum() > 0
        assert not any(np.any(edge) for edge in (alpha[0], alpha[-1], alpha[:, 0], alpha[:, -1])), 'actor crop truncates sprite'
        overlap = {}
        for mode in ('before', 'partial'):
            wall = images[f'{mode}-wall'][y:y + panel, x:x + panel]
            overlap[mode] = float(np.sum(alpha * wall) / np.sum(alpha))
        records.append({**actor, 'initialSpriteArea': round(float(alpha.sum()), 4),
                        'changedWallOverlapBefore': round(overlap['before'], 6),
                        'changedWallOverlapAfter': round(overlap['partial'], 6),
                        'delta': round(overlap['partial'] - overlap['before'], 6)})
    report = {'kind': 'Actual initial-pose actor sprite alpha against only the 27 changed wall drawings later in actual depth order',
              'actors': records, 'limitations': ['Other legacy walls/buildings/trees are not measured by this delta check',
                'No live GPU/browser or animation claim', 'Source alpha includes antialiased sprite edges; no visibility rule or fading is introduced']}
    (output / 'actor-wall-occlusion.json').write_text(json.dumps(report, indent=2) + '\n')
    draw_wall_crops(directory, output, records)
    return report


def draw_wall_crops(directory, output, records):
    changed = [actor for actor in records if actor['delta'] > 0]
    scene = ET.parse(directory / 'partial-full.svg').getroot()
    world = next(g for g in scene.iter(N + 'g') if g.get('class') == 'board-world')
    ox, oy = map(float, re.findall(r'-?\d+(?:\.\d+)?', world.get('transform'))[:2])
    board = Image.new('RGB', (560, len(changed) * 284), '#202624')
    draw = ImageDraw.Draw(board)
    before = Image.open(directory / 'before-full@1x.png').convert('RGB')
    after = Image.open(directory / 'partial-full@1x.png').convert('RGB')
    for row, actor in enumerate(changed):
        x, y = actor['cell']
        box = tuple(round(n) for n in (ox + x * 32 - 48, oy + y * 32 - 80, ox + x * 32 + 80, oy + y * 32 + 48))
        draw.text((12, row * 284 + 6), f"{actor['key']} | changed-wall overlap {actor['changedWallOverlapBefore']:.2%} -> {actor['changedWallOverlapAfter']:.2%}", fill='white')
        for col, image in enumerate((before, after)):
            board.paste(image.crop(box).resize((256, 256), Image.Resampling.NEAREST), (12 + col * 280, row * 284 + 24))
    board.save(output / 'actual-actor-clearance.png')


def review(sparse, dense, output):
    output.mkdir(parents=True, exist_ok=True)
    a = json.loads((sparse / 'provenance.json').read_text())
    b = json.loads((dense / 'provenance.json').read_text())
    checks = []
    for sampling, provenance in [(4, a), (16, b)]:
        before, after = provenance['captures']
        assert before['actorMarkupSha256'] == after['actorMarkupSha256']
        assert before['buildingMarkupSha256'] == after['buildingMarkupSha256']
        assert before['stateSha256'] == after['stateSha256']
        assert before['actors'] == after['actors'] == 24
        assert before['buildings'] == after['buildings'] == 18
        changes = [k for k, v in before['layerHashes'].items() if after['layerHashes'][k] != v]
        assert changes == ['scenery']
        assert all(after['sourcePngHashes'][k] == v for k, v in before['sourcePngHashes'].items())
        legacy = [w for w in after['wallBindings'] if w['status'] == 'legacy']
        old = {tuple(w['cell']): w for w in before['wallBindings']}
        assert len(legacy) == 41
        assert all(w == old[tuple(w['cell'])] for w in legacy)
        checks.append({'sampling': sampling, 'actorsIdentical': True, 'buildingsIdentical': True, 'stateIdentical': True,
            'changedLayers': changes, 'commonPngSourcesIdentical': len(before['sourcePngHashes']), 'legacyBindingsByteIdentical': len(legacy),
            'remainingLegacyGatehouses': sum(w['sourceIdentity'] == 'stone-gatehouse' for w in legacy)})
    assert a['captures'][0] == b['captures'][0]
    assert a['sources'] == b['sources']
    # Native source/core edge measurements disclose stock repetition; no source edits.
    root = Path(__file__).resolve().parents[2]
    material = root / 'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/fortification-materials'
    source_checks = []
    for name in ('stone-top-material', 'stone-wall-face-material'):
        path = material / name / 'master.png'
        native = np.asarray(Image.open(path).convert('RGB')).astype(float)
        core = native[115:1139, 115:1139]
        source_checks.append({'source': name, 'sha256': digest(path), 'dimensions': list(native.shape[:2][::-1]),
            'coreLeftRightMeanAbsoluteRgbDifference': float(np.mean(abs(core[:, 0] - core[:, -1]))),
            'coreTopBottomMeanAbsoluteRgbDifference': float(np.mean(abs(core[0] - core[-1]))),
            'sourceEdited': False, 'naturallySeamless': False})
    for pair, title in [(('before', 'partial'), 'production-vs-dense-pilot')]:
        images = [Image.open(dense / f'{name}-full-labeled@1x.png').convert('RGB') for name in pair]
        board = Image.new('RGB', (images[0].width * 2, images[0].height), '#202624')
        for i, image in enumerate(images): board.paste(image, (i * image.width, 0))
        board.save(output / f'{title}.png')
    images = [Image.open(p / 'partial-full-labeled@1x.png').convert('RGB') for p in (sparse, dense)]
    board = Image.new('RGB', (images[0].width * 2, images[0].height + 28), '#202624')
    draw = ImageDraw.Draw(board)
    for i, image in enumerate(images):
        draw.text((12 + i * image.width, 7), f'{[4, 16][i]} SOURCE PIXELS / WORLD UNIT', fill='white')
        board.paste(image, (i * image.width, 28))
    board.save(output / 'density-full-comparison.png')
    board = Image.new('RGB', (880, 520), '#303d34'); draw = ImageDraw.Draw(board)
    for row, directory in enumerate((sparse, dense)):
        draw.text((16, row * 260 + 8), f'{[4,16][row]} source pixels/world unit | actual 2x output | OPEN strips, endpoints outside view', fill='white')
        for col, name in enumerate(('open-ns', 'open-ew', 'open-double')):
            image = Image.open(directory / f'{name}@2x.png').convert('RGBA')
            board.paste(image, (20 + col * 280, row * 260 + 40), image)
            draw.text((20 + col * 280, row * 260 + 240), name, fill='white')
    board.save(output / 'density-strip-comparison.png')
    result = {'captureChecks': checks, 'sameBaselineAcrossDensities': True, 'sameNativeSourcesAcrossDensities': True,
              'sourceChecks': source_checks, 'newGateOrDoorPaint': False, 'productionEnabled': False}
    (output / 'verification.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('sparse', type=Path)
    parser.add_argument('dense', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--occlusion', action='store_true')
    args = parser.parse_args()
    print(json.dumps(review(args.sparse.resolve(), args.dense.resolve(), args.output.resolve()), indent=2))
    if args.occlusion:
        wall_occlusion(args.dense.resolve(), args.output.resolve())
