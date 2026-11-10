"""Reproduce the reviewed archer component selection without editing any files.

Requires Pillow, NumPy and SciPy. The runtime manifest owns live geometry;
this provenance check proves exact original RGBA selection from two sources.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]


def verify():
    review = json.loads((HERE / 'extraction-review.json').read_text())
    sources = []
    for source_record in review['sources']:
        path = HERE / source_record['file']
        if hashlib.sha256(path.read_bytes()).hexdigest() != source_record['sha256']:
            raise ValueError('Source differs from the reviewed generation.')
        rgba = np.array(Image.open(path).convert('RGBA'))
        labels, count = ndimage.label(rgba[:, :, 3] > 16)
        sizes = np.bincount(labels.ravel())
        ids = sorted((i for i in range(1, count + 1) if sizes[i] > 1000),
                     key=lambda i: np.where(labels == i)[1].min())
        if len(ids) != 6:
            raise ValueError('Expected six separate source figures.')
        labels = np.where(np.isin(labels, ids), labels, 0)
        distance, near = ndimage.distance_transform_edt(labels == 0, return_indices=True)
        owner = labels[near[0], near[1]]
        keep = (distance <= 3) & (rgba[:, :, 3] > 0)
        removed = (rgba[:, :, 3] > 0) & ~keep
        if int(removed.sum()) != source_record['removedPixels'] or int(rgba[:, :, 3][removed].max()) != source_record['maxRemovedAlpha']:
            raise ValueError('Only reviewed detached near-transparent noise may be removed.')
        sources.append((rgba, ids, owner, keep))
    native = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
    asset = next(a for a in native['assets'] if a.get('id') == 'archer-shot-v1')
    clip = native['actorClips']['archer.loose']
    intervals = [{'frame': asset['frameOrder'][i], 'span': span}
                 for i, span in zip(review['playbackFrames'], review['playbackSpansMs'])]
    if clip['frames'] != intervals or asset['presentationScale'] != review['presentationScale']:
        raise ValueError('Reviewed intervals or uniform scale changed.')
    if clip.get('releaseMs') != review['releaseMs'] or clip.get('showAccent') is not False or 'death' in clip:
        raise ValueError('Shot release, no-accent and no-death contract changed.')
    path = ROOT / 'godot' / asset['resourcePath'].removeprefix('res://')
    if hashlib.sha256(path.read_bytes()).hexdigest() != asset['sha256']:
        raise ValueError('Runtime PNG differs from its manifest hash.')
    actual = np.array(Image.open(path).convert('RGBA'))
    expected = np.zeros_like(actual)
    for index, frame in enumerate(review['frames']):
        if frame['sourceIndex'] != (1 if index == 4 else 0) or frame['sourceComponent'] != index:
            raise ValueError('Only recovery may select the corrected second source row.')
        rgba, ids, owner, keep = sources[frame['sourceIndex']]
        mask = keep & (owner == ids[frame['sourceComponent']])
        left, top, right, bottom = frame['sourceBounds']
        crop = rgba[top:bottom, left:right].copy()
        crop[~mask[top:bottom, left:right]] = 0
        if hashlib.sha256(crop.tobytes()).hexdigest() != frame['pixelSha256']:
            raise ValueError(f'Pose {index + 1} no longer contains original reviewed pixels.')
        x, y, width, height = frame['frameRegion']
        if crop.shape != (height, width, 4):
            raise ValueError('Source bounds disagree with packed rectangle.')
        expected[y:y + height, x:x + width] = crop
        if asset['frameRegions'][index] != frame['frameRegion'] or asset['frameAnchors'][index] != frame['frameAnchor']:
            raise ValueError('Live geometry differs from reviewed registration.')
    if not np.array_equal(expected, actual):
        raise ValueError('Runtime atlas is not exactly the selected original source RGBA.')
    print('Archer extraction passed: five v1 poses plus corrected v2 recovery; no resampling or repainting.')


if __name__ == '__main__':
    verify()
