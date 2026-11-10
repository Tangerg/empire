"""Reproduce reviewed archer breathing pixels and live catalog registration.

Requires Pillow, NumPy and SciPy. This is a read-only provenance check, not the
runtime geometry owner or a general-purpose asset generator.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]


def verify(runtime_pixels=None):
    review = json.loads((HERE / 'extraction-review.json').read_text())
    source_path = HERE / 'source-row.png'
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != review['sourceSha256']:
        raise ValueError('Source row differs from the reviewed generation.')
    source = np.array(Image.open(source_path).convert('RGBA'))
    labels, count = ndimage.label(source[:, :, 3] > 16)
    sizes = np.bincount(labels.ravel())
    ids = sorted((i for i in range(1, count + 1) if sizes[i] > 1000),
                 key=lambda i: np.where(labels == i)[1].min())
    if len(ids) != 4:
        raise ValueError('Expected four complete breathing drawings.')
    labels = np.where(np.isin(labels, ids), labels, 0)
    distance, nearest = ndimage.distance_transform_edt(labels == 0, return_indices=True)
    owner = labels[nearest[0], nearest[1]]
    keep = (distance <= 3) & (source[:, :, 3] > 0)
    removed = (source[:, :, 3] > 0) & ~keep
    if (int(removed.sum()) != review['removedPixels'] or
            int(source[:, :, 3][removed].max()) != review['maxRemovedAlpha']):
        raise ValueError('Only the reviewed detached near-transparent noise may be removed.')
    native = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
    asset = next(a for a in native['assets'] if a.get('id') == 'archer-breathe-v1')
    clip = native['actorClips']['archer.breathe']
    expected_frames = [{'frame': asset['frameOrder'][i], 'span': span}
                       for i, span in zip(review['playbackFrames'], review['playbackSpansMs'])]
    if (clip != {'unit': 'milliseconds', 'appearanceId': 'archer-breathe-v1', 'frames': expected_frames}
            or asset['presentationScale'] != review['presentationScale']):
        raise ValueError('Live breathing clip or uniform scale differs from review.')
    path = ROOT / 'godot' / asset['resourcePath'].removeprefix('res://')
    if hashlib.sha256(path.read_bytes()).hexdigest() != asset['sha256'] or asset['sha256'] != review['runtimePngSha256']:
        raise ValueError('Runtime PNG differs from the manifest or reviewed hash.')
    actual = np.array(Image.open(path).convert('RGBA')) if runtime_pixels is None else runtime_pixels
    expected = np.zeros((548, 1435, 4), dtype=np.uint8)
    if actual.shape != expected.shape:
        raise ValueError('Runtime dimensions differ from the reviewed atlas.')
    for index, (ident, frame) in enumerate(zip(ids, review['frames'])):
        if frame['sourceComponent'] != index:
            raise ValueError('Every original component must be retained in order.')
        mask = keep & (owner == ident)
        left, top, right, bottom = frame['sourceBounds']
        crop = source[top:bottom, left:right].copy()
        crop[~mask[top:bottom, left:right]] = 0
        if hashlib.sha256(crop.tobytes()).hexdigest() != frame['pixelSha256']:
            raise ValueError('Retained original RGBA pixels changed.')
        x, y, width, height = frame['frameRegion']
        if crop.shape != (height, width, 4):
            raise ValueError('Source bounds disagree with the packed rectangle.')
        expected[y:y + height, x:x + width] = crop
        if (asset['frameRegions'][index] != frame['frameRegion'] or
                asset['frameAnchors'][index] != frame['frameAnchor']):
            raise ValueError('Live rectangles or contact anchors need fresh review.')
    if not np.array_equal(expected, actual):
        raise ValueError('Runtime atlas is not the exact retained original RGBA.')
    print('Archer idle extraction passed: four drawings, no resizing or repainting; exact reviewed timing and registration.')


if __name__ == '__main__':
    verify()
