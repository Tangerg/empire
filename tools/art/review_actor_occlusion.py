"""Measure initial-pose actor silhouettes against later actual scene drawings."""
import argparse
from copy import deepcopy
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image
from render_ground_review import normalize_actor_facing
from svg_viewports import NS, flatten_viewports


def review(directory):
    provenance = json.loads((directory / 'runtime-scene-provenance.json').read_text())
    render = next(item for item in provenance['renders'] if item['filename'] == 'runtime-standing-full.svg')
    source = ET.parse(directory / render['filename']).getroot()
    world = source.find(f'{{{NS}}}g[@class="board-world"]')
    layer = world.find(f'{{{NS}}}g[@class="layer layer-units"]')
    drawings = list(layer)
    order = render['standingDrawOrder']
    assert len(drawings) == len(order)
    root = Path(__file__).resolve().parents[2]
    manifest = json.loads((root / 'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json').read_text())
    atlases = {Path(atlas['png2x']).name: atlas for atlas in manifest['atlases']}
    for metadata, drawing in zip(order, drawings):
        images = list(drawing.iter(f'{{{NS}}}image'))
        metadata['imageSources'] = [Path(image.get('href')).name for image in images]
        if 'actorId' not in metadata and images:
            atlas = atlases.get(Path(images[0].get('href')).name)
            if atlas:
                index = round(-float(images[0].get('x', 0)) / atlas['cellWidth']) + round(-float(images[0].get('y', 0)) / atlas['cellHeight']) * atlas['columns']
                metadata['environmentCellId'] = atlas['cellOrder'][index]
    # Keep the production frame as an occluder too; it is outside board-world.
    world_index = list(source).index(world)
    for index, drawing in enumerate(list(source)[world_index + 1:]):
        drawings.append(drawing)
        order.append({'id': f'frame:{index}', 'displayIndex': len(order), 'outsideWorld': True})
    masks = []
    with tempfile.TemporaryDirectory() as temporary:
        temporary = Path(temporary)
        for index, (drawing, metadata) in enumerate(zip(drawings, order)):
            # Measure silhouettes without the root-scoped scene grading/glow.
            # The full-scene PNGs retain that styling; these are diagnostic masks.
            svg = ET.Element(f'{{{NS}}}svg', {key: source.get(key) for key in ('width', 'height', 'viewBox')})
            for style in source.iter(f'{{{NS}}}style'):
                svg.append(deepcopy(style))
            for defs in source.iter(f'{{{NS}}}defs'):
                svg.append(deepcopy(defs))
            picture = deepcopy(drawing)
            if 'actorId' in metadata:
                # Measure the source sprite, excluding team rings, badges and contact shadows.
                for child in list(picture):
                    if child.get('class') != 'figure':
                        picture.remove(child)
                    else:
                        for part in list(child):
                            if part.get('class') != 'board-strip':
                                child.remove(part)
            container = ET.SubElement(svg, f'{{{NS}}}g', {} if metadata.get('outsideWorld') else {'transform': world.get('transform')})
            container.append(picture)
            for image in svg.iter(f'{{{NS}}}image'):
                href = image.get('href')
                if href and not href.startswith('data:'):
                    image.set('href', str((directory / href).resolve()))
            normalize_actor_facing(svg)
            defs = ET.SubElement(svg, f'{{{NS}}}defs')
            flatten_viewports(svg, defs)
            input_path, output_path = temporary / f'{index}.svg', temporary / f'{index}.png'
            ET.ElementTree(svg).write(input_path, encoding='unicode')
            subprocess.run(['inkscape', str(input_path), '--export-type=png', f'--export-filename={output_path}', '--export-width=852'],
                           env={**os.environ, 'INKSCAPE_PROFILE_DIR': str(temporary / 'profile')}, check=True, capture_output=True)
            masks.append(np.asarray(Image.open(output_path).convert('RGBA').getchannel('A'), dtype=float) / 255)
    actors = []
    for index, metadata in enumerate(order):
        if 'actorId' not in metadata:
            continue
        silhouette = masks[index]
        area = float(silhouette.sum())
        remaining = np.ones(silhouette.shape)
        covering = []
        for later in range(index + 1, len(order)):
            overlap = float((silhouette * masks[later]).sum()) / area
            remaining *= 1 - masks[later]
            if overlap >= 0.001:
                covering.append({**order[later], 'pairwiseSilhouetteOverlapFraction': round(overlap, 5)})
        actors.append({**metadata, 'sourceSilhouettePixelArea': round(area, 2),
                       'occludedFraction': round(1 - float((silhouette * remaining).sum()) / area, 5),
                       'coveringObjects': covering})
    result = {'kind': 'offline 1x alpha-mask diagnostic of actual initial-pose scene drawings',
              'levelId': provenance['levelId'], 'actorCount': len(actors), 'actors': actors,
              'method': 'Actor sprite only, excluding team rings/badges/contact shadows and root-scoped scene grading/glow. Alpha-weighted overlap with every later drawing, with actual source clipping and facing. Pairwise fractions overlap and must not be added. Entries below 0.1% are omitted; total includes them.',
              'limitations': ['Not browser/GPU pixel verification', 'Initial pose only; visibility changes during movement and animation',
                              'Masks count source alpha, including semitransparent sprite edge/shadow pixels; they are diagnostic, not a gameplay visibility rule']}
    (directory / 'actor-occlusion.json').write_text(json.dumps(result, indent=2) + '\n')
    for actor in actors:
        if actor['occludedFraction'] >= 0.01:
            print(actor['id'], actor.get('key'), actor['worldFoot'], f"{actor['occludedFraction']:.1%}", [(item['id'], item.get('environmentCellId'), item['pairwiseSilhouetteOverlapFraction']) for item in actor['coveringObjects']])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    review(parser.parse_args().directory.resolve())
