"""Verify a registered map-structure variant without publishing or changing pixels."""
import argparse
import hashlib
import json
import math
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')
STATES = ['normal', 'damaged', 'captured']


def bounds_at(alpha, threshold):
    return alpha.point(lambda value: 255 if value >= threshold else 0).getbbox()


def verify(entry, export_dir, world_support, root=ROOT, alpha_threshold=2):
    """World support includes shadow; alpha=1 is separately reported native dust."""
    if not 1 <= alpha_threshold <= 255:
        raise ValueError('alpha threshold must be in [1,255]')
    if len(world_support) != 4 or world_support[0] >= world_support[2] or world_support[1] >= world_support[3]:
        raise ValueError('invalid world support')
    if entry['frames'] != 3 or entry['stateOrder'] != STATES or entry['pixelDensity'] != 2:
        raise ValueError('three normal/damaged/captured states and HD density two required')
    fw, fh = entry['frameWidth'], entry['frameHeight']
    if entry['hdWidth'] != fw * 6 or entry['hdHeight'] != fh * 2:
        raise ValueError('inconsistent logical/HD geometry')
    sequence = json.loads((export_dir / 'sequence.json').read_text())
    config = sequence['config']
    if list(config['frames']) != STATES or config['frameSize'] != [fw, fh] or sequence['logicalPivot'] != entry['anchor']:
        raise ValueError('registered geometry/state order differs from approved export')
    if sequence['independentFrameFit'] is not False or sequence['gameplayFootprintChanged'] is not False:
        raise ValueError('source registration may not independently fit frames or change rules')
    reports = sequence['frames']
    if [(item['frame'], item['density']) for item in reports] != [(state, density) for state in STATES for density in (1, 2)]:
        raise ValueError('complete ordered native/HD source provenance required')
    source_scale, origin, source_foot = config['scale'], config['origin'], config['sourceFoot']
    if not math.isfinite(source_scale) or source_scale <= 0 or len(origin) != 2 or len(source_foot) != 2:
        raise ValueError('invalid shared source transform')
    pivot = [origin[axis] + source_foot[axis] * source_scale for axis in (0, 1)]
    if any(abs(pivot[axis] - entry['anchor'][axis]) > 1e-8 for axis in (0, 1)):
        raise ValueError('pivot differs from registered source contact')
    hashes = {}
    for report in reports:
        path = config['frames'][report['frame']]
        if report['source'] != path:
            raise ValueError('source path disagrees with registration')
        digest = hashlib.sha256((root / path).read_bytes()).hexdigest()
        if digest != report['sourceSha256']:
            raise ValueError('native source provenance hash mismatch')
        with Image.open(root / path) as source:
            if source.mode != 'RGBA':
                raise ValueError('native source must be RGBA')
            offset = config.get('registrationOffsets', {}).get(report['frame'], [0, 0])
            if report['registrationOffset'] != offset:
                raise ValueError('source offset disagrees with registration')
            if any(origin[axis] + offset[axis] < 0 or origin[axis] + offset[axis] + source.size[axis] * source_scale > [fw, fh][axis] + 1e-8 for axis in (0, 1)):
                raise ValueError('shared transform clips full native source canvas')
        hashes[report['frame']] = digest
    if len(set(hashes.values())) != 3:
        raise ValueError('source trio must have distinct native frames')
    result = []
    scale = entry['presentationScale']
    ax, ay = entry['anchor']
    for density, runtime_path in [(1, entry['png']), (2, entry['hdPng'])]:
        data = (root / PACK / runtime_path).read_bytes()
        if data != (export_dir / f'sheet@{density}x.png').read_bytes():
            raise ValueError('runtime sheet differs from approved export bytes')
        with Image.open(root / PACK / runtime_path) as image:
            if image.mode != 'RGBA' or image.size != (fw * 3 * density, fh * density):
                raise ValueError('RGBA sheet geometry differs from registration')
            for index, state in enumerate(STATES):
                alpha = image.crop((index * fw * density, 0, (index + 1) * fw * density, fh * density)).getchannel('A')
                w, h = alpha.size
                edge = max(alpha.crop(box).getextrema()[1] for box in [(0, 0, w, 1), (0, h-1, w, h), (0, 0, 1, h), (w-1, 0, w, h)])
                if edge:
                    raise ValueError('derived frame must retain a fully transparent perimeter')
                measured = {}
                for threshold in sorted({1, 2, 128, alpha_threshold}):
                    bounds = bounds_at(alpha, threshold)
                    if bounds is None:
                        raise ValueError('empty structure state')
                    world = [(bounds[0] / density - ax) * scale, (bounds[1] / density - ay) * scale,
                             (bounds[2] / density - ax) * scale, (bounds[3] / density - ay) * scale]
                    measured[str(threshold)] = {'pixelBounds': bounds, 'worldBounds': world}
                actual = measured[str(alpha_threshold)]['worldBounds']
                if actual[0] < world_support[0] - 1e-8 or actual[1] < world_support[1] - 1e-8 or actual[2] > world_support[2] + 1e-8 or actual[3] > world_support[3] + 1e-8:
                    raise ValueError(f'{state}@{density}x alpha>={alpha_threshold} exceeds world support: {actual}')
                result.append({'state': state, 'density': density, 'edgeMax': edge, 'alphaBounds': measured})
    return {'id': entry['id'], 'topicId': entry['topicId'], 'canonicalTopicsAdded': 0,
            'worldSupport': world_support, 'enforcedAlphaThreshold': alpha_threshold,
            'sourceSha256': hashes, 'frames': result, 'liveGameVerified': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('id')
    parser.add_argument('export_dir', type=Path)
    parser.add_argument('--world-support', type=float, nargs=4, required=True)
    parser.add_argument('--alpha-threshold', type=int, default=2)
    args = parser.parse_args()
    document = json.loads((ROOT / PACK / 'manifest-map-structure-presentations.json').read_text())
    matches = [entry for entry in document['variants'] if entry['id'] == args.id]
    if len(matches) != 1:
        raise ValueError('exactly one activated presentation required')
    print(json.dumps(verify(matches[0], args.export_dir.resolve(), args.world_support, alpha_threshold=args.alpha_threshold), indent=2))
