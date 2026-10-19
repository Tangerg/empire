#!/usr/bin/env python3
"""Deterministic real-asset art comparison; not a gameplay capture."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image
from export_overrides import ROOT, read_json, records
from export_environment_overrides import PACK


def place(canvas, sprite, point, anchor, scale=1, density=2):
    # Sprite pixels are already at density2; only scenery scale is resampled.
    image = sprite if scale == 1 else sprite.resize((round(sprite.width * scale), round(sprite.height * scale)), Image.Resampling.LANCZOS)
    xy = [round((point[i] - anchor[i] * scale) * density) for i in range(2)]
    if xy[0] < 0 or xy[1] < 0 or xy[0] + image.width > canvas.width or xy[1] + image.height > canvas.height:
        raise ValueError('preview placement clips an asset canvas')
    canvas.alpha_composite(image, tuple(xy))
    return {'pointLogical': list(point), 'anchorLogical': list(anchor), 'scale': scale,
            'destinationPixels': xy, 'renderedSize': list(image.size)}


def build(root, output):
    pack = root / PACK
    env = pack / 'environment-builder-v1'
    known = records(root)
    manifest = read_json(env / 'manifest-environment-builder-v1.json')
    atlases = {a['id']: a for a in manifest['atlases']}
    evidence = []
    inputs = {}

    def load(path):
        raw = path.read_bytes()
        inputs[str(path.relative_to(root))] = hashlib.sha256(raw).hexdigest()
        with Image.open(path) as opened:
            return opened.convert('RGBA')

    def topic(topic_id, frame=0):
        own_pack, record, hd = known[topic_id]
        if hd is None:
            raise ValueError('comparison expects actual density2 export')
        image = load(own_pack / hd['png'])
        fw, fh = record.get('frameWidth', record['width']), record.get('frameHeight', record['height'])
        if image.size != (record['width'] * 2, record['height'] * 2):
            raise ValueError('unexpected HD topic dimensions')
        return image.crop((frame * fw * 2, 0, (frame + 1) * fw * 2, fh * 2)), record['anchor']

    def environment(atlas_id, cell_id):
        atlas = atlases[atlas_id]
        image = load(env / atlas['png2x'])
        index = atlas['cellOrder'].index(cell_id)
        w, h = atlas['cellWidth2x'], atlas['cellHeight2x']
        x, y = index % atlas['columns'] * w, index // atlas['columns'] * h
        return image.crop((x, y, x + w, y + h)), atlas.get('anchor')

    ground, _ = environment('surface-meadow', 'meadow-1')
    if ground.size != (64, 64):
        raise ValueError('expected32logical ground tile')
    variants = [('normal-idle', 0, 0), ('damaged-walk-a', 1, 1), ('captured-walk-b', 2, 3)]
    panels = []
    for label, structure_state, unit_pose in variants:
        canvas = Image.new('RGBA', (640, 448))
        for y in range(7):
            for x in range(10):
                canvas.alpha_composite(ground, (x * 64, y * 64))
        placements = []
        # Ground, scenery, structures, units: follows board-surface layer order.
        # Within scenery, explicit ascending y gives a stable comparison order.
        for cell, point, scale in [('oak-ancient', (56, 105), .8), ('mixed-forest-dense', (264, 115), .8), ('fern-bed', (80, 180), .64)]:
            image, anchor = environment('forest-temperate', cell)
            placements.append({'id': cell, 'layer': 'scenery', **place(canvas, image, point, anchor, scale)})
        image, anchor = topic('C01-BPROP-COVER-1')
        placements.append({'id': 'C01-BPROP-COVER-1', 'layer': 'scenery', **place(canvas, image, (250, 180), anchor, .7)})
        image, anchor = topic('C01-STRUCT-OATH-TOWER-CONSOLE', structure_state)
        placements.append({'id': 'C01-STRUCT-OATH-TOWER-CONSOLE', 'stateIndex': structure_state, 'layer': 'structures', **place(canvas, image, (160, 115), anchor)})
        for unit, point in [('C01-UNIT-SWORDSMAN', (135, 174)), ('C01-UNIT-ARCHER', (185, 174))]:
            image, anchor = topic(unit, unit_pose)
            placements.append({'id': unit, 'poseIndex': unit_pose, 'layer': 'units', **place(canvas, image, point, anchor)})
        panels.append(canvas)
        evidence.append({'panel': label, 'placements': placements})
    hd = Image.new('RGBA', (640 * len(panels), 448))
    for i, panel in enumerate(panels):
        hd.alpha_composite(panel, (i * 640, 0))
    native = hd.resize((hd.width // 2, hd.height // 2), Image.Resampling.LANCZOS)
    output.mkdir(parents=True, exist_ok=True)
    hd.save(output / 'cohesion-2x.png')
    native.save(output / 'cohesion-native.png')
    catalog = read_json(root / 'tools/art/surface-overrides.json')
    report = {
        'kind': 'deterministic-real-asset-comparison-not-gameplay',
        'groundStatus': 'remade' if 'surface-meadow' in catalog['atlases'] else 'legacy-pending-remake',
        'panelOrder': [v[0] for v in variants], 'logicalPanelSize': [320, 224], 'density': 2,
        'inputsSha256': inputs,
        'contractsSha256': {str(path.relative_to(root)): hashlib.sha256(path.read_bytes()).hexdigest() for path in (
            pack / 'manifest-final-fantasy-v1.json', pack / 'manifest-tactical-runtime-hd.json',
            env / 'manifest-environment-builder-v1.json', root / 'tools/art/source-overrides.json',
            root / 'tools/art/frame-overrides.json', root / 'tools/art/environment-overrides.json',
            root / 'tools/art/surface-overrides.json')},
        'panels': evidence,
        'placementEvidence': [
            'game-ui/src/art/board-surface.ts: TILE32; ground/scenery/structures/units/foreground layer order',
            'game-ui/src/art/runtime-raster.ts: unit footing at tile16,31 minus manifest anchor',
            'story-candidate-01/src/presentation/candidate-01-runtime.ts: structure tile16,31 minus anchor',
            'story-candidate-01/src/presentation/candidate-01-map-scene.ts: scenery uses manifest anchor; standing point at cell+.5,+1.18',
            'story-candidate-01/src/presentation/candidate-01-terrain-materials.ts: forest scale .74 plus hash jitter up to .12'],
        'comparisonChoices': ['authored test arrangement, not a particular live level', 'canopy fixed .8 within .74-.86 renderer range; fern .64 within .58-.70 understory range', 'crate .7 is an explicit comparison choice, not a verified level placement', 'ascending scenery y is explicit comparison ordering, not a claim of global engine y-sort'],
        'omittedRuntimeEffects': ['team-color multiply tint', 'contact shadows and team rings', 'CSS standing shadows', 'field sunlight overlays', 'camera transforms and subpixel SVG clipping', 'animation timing and state-switch behavior'],
        'acceptance': 'visual review required; actual gameplay pending',
        'outputs': {name: {'file': name, 'sha256': hashlib.sha256((output / name).read_bytes()).hexdigest()} for name in ('cohesion-native.png', 'cohesion-2x.png')}
    }
    (output / 'cohesion-placement.json').write_text(json.dumps(report, indent=2) + '\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(build(args.root.resolve(), args.output_dir.resolve()), indent=2))
