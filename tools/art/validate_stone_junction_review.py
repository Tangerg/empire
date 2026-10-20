"""Check actual SVG rasters, source-preserving windows and the all-level scope fence."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image


def sha(value):
    return hashlib.sha256(value.encode() if isinstance(value, str) else value).hexdigest()


def validate(directory):
    isolated = directory / 'isolated'
    review = json.loads((isolated / 'junction-review.json').read_text())
    x0, y0, width, height = review['viewBox']
    rasters = []
    for density in (1, 2):
        composite = np.asarray(Image.open(isolated / f'junction@{density}x.png').convert('RGBA'))
        h, w = composite.shape[:2]
        xs, ys = np.meshgrid(x0 + (np.arange(w) + .5) * width / w,
                             y0 + (np.arange(h) + .5) * height / h)
        edge = max(int(composite[0, :, 3].max()), int(composite[-1, :, 3].max()),
                   int(composite[:, 0, 3].max()), int(composite[:, -1, 3].max()))
        if edge > 1:
            raise AssertionError('final composite edge exceeds source alpha-1 tolerance')
        reports = {}
        for axis in ('horizontal', 'vertical'):
            complete = np.asarray(Image.open(isolated / f'{axis}-complete@{density}x.png').convert('RGBA'))
            clipped = np.asarray(Image.open(isolated / f'{axis}-clipped@{density}x.png').convert('RGBA'))
            margin = max(width / w, height / h)
            affected = np.zeros((h, w), dtype=bool)
            interior = np.zeros((h, w), dtype=bool)
            for x, y, rw, rh in review['windows'][axis]:
                affected |= (xs >= x - margin) & (xs <= x + rw + margin) & (ys >= y - margin) & (ys <= y + rh + margin)
                interior |= (xs > x + margin) & (xs < x + rw - margin) & (ys > y + margin) & (ys < y + rh - margin)
            difference = np.abs(complete.astype(int) - clipped.astype(int))
            # The approved deck is alpha 253. Inkscape's clipped image surface
            # rounds 51 horizontal 2x pixels by one RGBA level (four are edge
            # alpha), despite unchanged source bytes and fully open geometry.
            # Native exporter tests enforce exact source-derived RGBA; this is
            # an explicit rendered 8-bit resampling tolerance, not source edits.
            outside_delta = int(difference[~affected].max())
            if outside_delta > 1:
                raise AssertionError(f'{axis}: source appearance changed beyond 8-bit resampling precision')
            if clipped[interior, 3].max() != 0 or complete[interior, 3].max() < 240:
                raise AssertionError(f'{axis}: a required rail opening is missing')
            reports[axis] = {'outsideWindowsMaximumRgbaDelta': outside_delta,
                             'outsideWindowsDifferingPixels': int(np.any(difference[~affected], axis=1).sum()),
                             'interiorCutAlpha': 0,
                             'antialiasBoundaryWorldPx': margin,
                             'completePixelSha256': sha(complete.tobytes()), 'clippedPixelSha256': sha(clipped.tobytes())}
        vertical = np.asarray(Image.open(isolated / f'vertical-clipped@{density}x.png').convert('RGBA'))
        # Both real channels are three cells wide. No new strong vertical pixels
        # may extend beyond them; native alpha-1 canvas residue is not repainted.
        dry_alpha = int(vertical[(xs < -48) | (xs >= 48), 3].max())
        if dry_alpha > 1:
            raise AssertionError('new vertical source places opaque pixels on adjacent dry terrain')
        def sample(x, y):
            return int(composite[min(h - 1, int((y - y0) * h / height)),
                                 min(w - 1, int((x - x0) * w / width)), 3])
        through_alpha = min([sample(x, 0) for x in range(-48, 49)]
                            + [sample(0, y) for y in range(-48, 49)])
        if through_alpha < 240:
            raise AssertionError('junction has a transparent gap along a required deck axis')
        rasters.append({'density': density, 'finalEdgeMaxAlpha': edge, 'dryNeighborMaxNewAlpha': dry_alpha,
                        'throughDeckMinimumAlpha': through_alpha, 'sources': reports})
    before = json.loads((directory / 'before/runtime-consumers.json').read_text())
    after = json.loads((directory / 'after/runtime-consumers.json').read_text())
    levels = []
    for old, new in zip(before['levels'], after['levels'], strict=True):
        unchanged_state = old['stateSha256'] == new['stateSha256'] and old['levelSha256'] == new['levelSha256']
        unchanged_projection = old['projectionSha256'] == new['projectionSha256']
        if not unchanged_state or old['id'] != new['id'] or (old['id'] != 'c01-12' and not unchanged_projection):
            raise AssertionError('the bounded crossing change escaped its level/state fence')
        if old['id'] == 'c01-12' and any(old['projectionSha256'][layer] != new['projectionSha256'][layer]
                                       for layer in ('ground', 'overUnits')):
            raise AssertionError('c01-12 changed outside crossing scenery')
        levels.append({'id': old['id'], 'stateSha256': old['stateSha256'], 'stateUnchanged': unchanged_state,
                       'fullProjectionUnchanged': unchanged_projection})
    roots = [ET.parse(directory / side / 'runtime-standing-full.svg').getroot() for side in ('before', 'after')]
    unit_layers = [next(node for node in root.iter() if node.get('class') == 'layer layer-units') for root in roots]
    standing = [ET.tostring(layer) for layer in unit_layers]
    if standing[0] != standing[1]:
        raise AssertionError('standing drawings/order, actors, terrain buildings or targets changed')
    actors = [[node for node in layer if 'unit' in node.get('class', '').split()] for layer in unit_layers]
    structures = [[node for node in root.iter() if node.get('data-candidate-art') == 'structure'] for root in roots]
    targets = [[node for node in group if any('struct-sunken-bell-sluice' in image.get('href', '') for image in node.iter())]
               for group in structures]
    woods = [[node for node in root.iter() if node.get('class') == 'candidate-environment-prop is-standing'
              and any('crossings-fortifications@2x.png' in image.get('href', '') for image in node.iter())] for root in roots]
    # Only the first two (northern) legacy drawings remain after this batch.
    wood_before = b''.join(ET.tostring(node) for node in woods[0][:2])
    wood_after = b''.join(ET.tostring(node) for node in woods[1])
    if wood_before != wood_after or len(woods[1]) != 2:
        raise AssertionError('deferred northern wood changed')
    if [len(actors[1]), len(structures[1]), len(targets[1])] != [19, 9, 3]:
        raise AssertionError('complete board must retain 19 actors, six terrain buildings and three targets')
    sources = [json.loads((directory / side / 'runtime-scene-provenance.json').read_text())['sources'] for side in ('before', 'after')]
    old_pngs = {path: digest for path, digest in sources[0].items() if path.endswith('.png')}
    if any(sources[1].get(path) != digest for path, digest in old_pngs.items()):
        raise AssertionError('an existing source raster changed')
    result = {
        'status': 'offline SVG source/projection/markup gates passed; no browser/GPU/playable claim',
        'openingWorldPixels': {'eastWestDeckHeight': 12, 'northSouthDeckWidth': 34},
        'sourceWindowCoordinates': {axis: [[(x + review[axis]['anchor'][0]) / review[axis]['logicalPerSourcePixel'],
                                           (y + review[axis]['anchor'][1]) / review[axis]['logicalPerSourcePixel'],
                                           w / review[axis]['logicalPerSourcePixel'], h / review[axis]['logicalPerSourcePixel']]
                                          for x, y, w, h in review['windows'][axis]]
                                    for axis in ('horizontal', 'vertical')},
        'rasters': rasters, 'levels': levels, 'standingLayerSha256': sha(standing[0]),
        'actorMarkupSha256': sha(b''.join(ET.tostring(node) for node in actors[0])),
        'structureMarkupSha256': sha(b''.join(ET.tostring(node) for node in structures[0])),
        'targetMarkupSha256': sha(b''.join(ET.tostring(node) for node in targets[0])),
        'northWoodMarkupSha256': sha(wood_after),
        'actors': 19, 'terrainBuildings': 6, 'targets': 3, 'existingRasterSourcesUnchanged': len(old_pngs),
        'southernStoneDrawingsReplaced': 2, 'northernWoodDrawingsPending': 2,
        'limitations': ['Offline alpha and SVG validation does not establish live GPU rasterization or gameplay interactions',
                        'The north watergate/wood stacks remain unchanged and unaccepted',
                        'Native north/south end steps and approved horizontal end posts are retained; no new bank landing is invented'],
    }
    (directory / 'validation.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    print(json.dumps(validate(args.directory.resolve()), indent=2))
