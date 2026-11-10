"""Read-only reproduction of the reviewed Archer fall component extraction.

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


def require_original_pixels(expected, actual):
    if not np.array_equal(expected, actual):
        raise ValueError('Runtime atlas is not the reviewed, unpainted source components.')


def verify():
    review = json.loads((HERE / 'extraction-review.json').read_text())
    source_path = HERE / 'source-row.png'
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != review['sourceSha256']:
        raise ValueError('Source row differs from the reviewed generation.')
    source = np.array(Image.open(source_path).convert('RGBA'))
    labels, count = ndimage.label(source[:, :, 3] > 16)
    sizes = np.bincount(labels.ravel())
    ids = sorted((ident for ident in range(1, count + 1) if sizes[ident] > 1000),
                 key=lambda ident: np.where(labels == ident)[1].min())
    if len(ids) != 6:
        raise ValueError('Expected six reviewed fall source figures.')
    labels = np.where(np.isin(labels, ids), labels, 0)
    distance, nearest = ndimage.distance_transform_edt(labels == 0, return_indices=True)
    owner = labels[nearest[0], nearest[1]]
    keep = (distance <= 3) & (source[:, :, 3] > 0)
    removed = (source[:, :, 3] > 0) & ~keep
    if int(removed.sum()) != review['removedPixels'] or (int(source[:, :, 3][removed].max()) if removed.any() else 0) != review['maxRemovedAlpha']:
        raise ValueError('Extraction may remove only the reviewed detached near-transparent noise.')
    native = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
    asset = next(record for record in native['assets'] if record.get('id') == 'archer-fall-v1')
    clip = native['actorClips']['archer.fall']
    expected_frames = [{'frame': asset['frameOrder'][index], 'span': span}
                       for index, span in zip(review['playbackFrames'], review['playbackSpansMs'])]
    if clip['frames'] != expected_frames or asset['presentationScale'] != review['presentationScale']:
        raise ValueError('Live fall intervals or uniform scale changed and need a fresh review.')
    if clip.get('death') != {'mode': 'after-clip', 'fadeMs': 160}:
        raise ValueError('Reviewed post-collapse fade policy changed.')
    path = ROOT / 'godot' / asset['resourcePath'].removeprefix('res://')
    if hashlib.sha256(path.read_bytes()).hexdigest() != asset['sha256']:
        raise ValueError('Runtime PNG differs from the manifest hash.')
    if hashlib.sha256(path.read_bytes()).hexdigest() != review['runtimePngSha256']:
        raise ValueError('Runtime PNG differs from the reviewed atlas.')
    actual = np.array(Image.open(path).convert('RGBA'))
    expected = np.zeros_like(actual)
    if len(review['frames']) != 6 or review['playbackFrames'] != list(range(6)):
        raise ValueError('Expected six independently reviewed atlas frames in order.')
    selected = ids
    for index, (ident, frame) in enumerate(zip(selected, review['frames'])):
        mask = keep & (owner == ident)
        left, top, right, bottom = frame['sourceBounds']
        ys, xs = np.where(mask)
        full_bounds = [int(xs.min()) - 4, int(ys.min()) - 4, int(xs.max()) + 5, int(ys.max()) + 5]
        if frame['sourceBounds'] != full_bounds:
            raise ValueError('Source bounds must retain the complete component plus four transparent pixels.')
        if frame['frameAnchor'] != [frame['sourceAnchor'][0] - left, frame['sourceAnchor'][1] - top]:
            raise ValueError('Source and atlas support anchors disagree.')
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
    require_original_pixels(expected, actual)
    altered = actual.copy()
    y, x = np.argwhere(altered[:, :, 3] > 16)[0]
    altered[y, x, 0] ^= 1
    try:
        require_original_pixels(expected, altered)
    except ValueError:
        pass
    else:
        raise ValueError('Pixel equality guard accepted a one-channel mutation.')
    print('One-channel in-memory pixel mutation correctly rejected.')
    print('Archer extraction passed: six original RGBA components, full silhouettes, no rescaling or redrawing.')
    print(f'Removed {int(removed.sum())} detached pixels with maximum alpha {review['maxRemovedAlpha']}/255.')


if __name__ == '__main__':
    verify()
