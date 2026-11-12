"""Read-only reproduction of the reviewed Torren component extraction.

Requires Pillow, NumPy and SciPy. This is an art provenance check, not a runtime
asset generator or a second catalog. The native manifest owns live geometry.
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
    source_path = HERE / 'source-sheet.png'
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != review['sourceSha256']:
        raise ValueError('Source row differs from the reviewed generation.')
    source = np.array(Image.open(source_path).convert('RGBA'))
    labels, count = ndimage.label(source[:, :, 3] > 16)
    sizes = np.bincount(labels.ravel())
    ids = [ident for ident in range(1, count + 1) if sizes[ident] > 10000]
    if len(ids) != 8:
        raise ValueError('Expected exactly eight isolated substantial alpha components.')
    ids.sort(key=lambda ident: (int(np.where(labels == ident)[0].mean()) // 443, int(np.where(labels == ident)[1].mean())))
    labels = np.where(np.isin(labels, ids), labels, 0)
    distance, nearest = ndimage.distance_transform_edt(labels == 0, return_indices=True)
    owner = labels[nearest[0], nearest[1]]
    keep = (distance <= 3) & (source[:, :, 3] > 0)
    removed = (source[:, :, 3] > 0) & ~keep
    if int(removed.sum()) != review['removedPixels'] or int(source[:, :, 3][removed].max()) != review['removedMaxAlpha']:
        raise ValueError('Extraction may remove only the reviewed detached near-transparent noise.')
    native = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
    asset = next(record for record in native['assets'] if record.get('id') == 'torren-walk-v1')
    path = ROOT / 'godot' / asset['resourcePath'].removeprefix('res://')
    if hashlib.sha256(path.read_bytes()).hexdigest() != asset['sha256']:
        raise ValueError('Runtime PNG differs from the manifest hash.')
    actual = np.array(Image.open(path).convert('RGBA'))
    expected = np.zeros_like(actual)
    for index, (ident, frame) in enumerate(zip(ids, review['frames'])):
        mask = keep & (owner == ident)
        left, top, right, bottom = frame['sourceBounds']
        crop = source[top:bottom, left:right].copy()
        crop[~mask[top:bottom, left:right]] = 0
        if hashlib.sha256(crop.tobytes()).hexdigest() != frame['pixelSha256']:
            raise ValueError(f'Pose {index + 1} has changed original pixels.')
        x, y, width, height = frame['frameRegion']
        if crop.shape != (height, width, 4):
            raise ValueError('Recorded component bounds disagree with the packed rectangle.')
        expected[y:y + height, x:x + width] = crop
        if asset['frameRegions'][index] != frame['frameRegion'] or asset['frameAnchors'][index] != frame['frameAnchor']:
            raise ValueError('Live rectangles/anchors changed and need a fresh review.')
    if not np.array_equal(expected, actual):
        raise ValueError('Runtime atlas is not the reviewed, unpainted source components.')
    print('Torren extraction passed: eight original RGBA components, full silhouettes, no rescaling or redrawing.')
    print(f'Removed {int(removed.sum())} detached pixels with maximum alpha 8/255.')


if __name__ == '__main__':
    verify()
