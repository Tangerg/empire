"""Verify the proposed gravekeeper idle atlas against its original generation.

Read-only QA verification. Requires Pillow, NumPy and SciPy. No production
manifest is assumed; integration must separately verify live clip registration.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent


def digest(data):
    return hashlib.sha256(data).hexdigest()


def verify(runtime_pixels=None, announce=True):
    review = json.loads((HERE / 'extraction-review.json').read_text())
    source_path = HERE / review['source']
    if digest(source_path.read_bytes()) != review['sourceSha256']:
        raise ValueError('Original generation PNG has changed.')
    if digest((HERE / review['prompt']).read_bytes()) != review['promptSha256']:
        raise ValueError('Generation prompt has changed.')
    source = np.array(Image.open(source_path).convert('RGBA'))
    if digest(source.tobytes()) != review['sourceRgbaSha256']:
        raise ValueError('Original generation RGBA has changed.')
    labels, count = ndimage.label(source[:, :, 3] > 16)
    sizes = np.bincount(labels.ravel())
    ids = sorted((i for i in range(1, count + 1) if sizes[i] > 1000),
                 key=lambda i: np.where(labels == i)[1].min())
    if len(ids) != 4:
        raise ValueError('Expected four independently drawn complete components.')
    labels = np.where(np.isin(labels, ids), labels, 0)
    distance, nearest = ndimage.distance_transform_edt(labels == 0, return_indices=True)
    owner = labels[nearest[0], nearest[1]]
    keep = (distance <= 3) & (source[:, :, 3] > 0)
    removed = (source[:, :, 3] > 0) & ~keep
    max_removed_alpha = int(source[:, :, 3][removed].max()) if removed.any() else 0
    if (int(removed.sum()) != review['removedPixels'] or
            max_removed_alpha != review['maxRemovedAlpha'] or max_removed_alpha > 9):
        raise ValueError('Removed pixels differ from reviewed detached alpha noise.')
    if (review['significantComponentAlphaThresholdExclusive'] != 16 or
            review['significantComponentPixelCountExclusive'] != 1000 or
            review['retainedAlphaFringeRadius'] != 3 or
            review['transparentPaddingPixels'] != 12):
        raise ValueError('Extraction parameters differ from review.')
    if (review['playbackFrames'] != [0, 1, 2, 3] or
            review['frameOrder'] != ['breathe-rest', 'breathe-inhale', 'breathe-high', 'breathe-exhale'] or
            review['playbackSpansMs'] != [700, 500, 500, 700] or
            review['presentationScale'] != 0.0792 or review['durationMs'] != 2400):
        raise ValueError('Proposed timing/scale differs from the agreed art contract.')
    path = HERE / review['runtimePng']
    if digest(path.read_bytes()) != review['runtimePngSha256']:
        raise ValueError('Proposed atlas PNG hash differs from review.')
    actual = np.array(Image.open(path).convert('RGBA')) if runtime_pixels is None else runtime_pixels
    width, height = review['runtimeDimensions']
    expected = np.zeros((height, width, 4), dtype=np.uint8)
    if actual.shape != expected.shape:
        raise ValueError('Atlas dimensions differ from review.')
    cursor = 0
    for index, (ident, frame) in enumerate(zip(ids, review['frames'])):
        if frame['sourceComponent'] != index or frame['frameName'] != review['frameOrder'][index]:
            raise ValueError('Every original component must be retained in source order.')
        mask = keep & (owner == ident)
        ys, xs = np.where(mask)
        bounds = [int(xs.min())-12, int(ys.min())-12, int(xs.max())+13, int(ys.max())+13]
        if frame['sourceBounds'] != bounds:
            raise ValueError('Crop must preserve twelve transparent padding pixels.')
        left, top, right, bottom = bounds
        crop = source[top:bottom, left:right].copy()
        crop[~mask[top:bottom, left:right]] = 0
        if digest(crop.tobytes()) != frame['pixelSha256']:
            raise ValueError('Retained original component RGBA has changed.')
        if frame['retainedPixels'] != int(mask.sum()):
            raise ValueError('Retained component pixel count has changed.')
        x, y, w, h = frame['frameRegion']
        if [x, y, w, h] != [cursor, 0, right-left, bottom-top]:
            raise ValueError('Packed rectangle differs from the retained source crop.')
        anchor = [303 + index * 542, 598]
        if (frame['sourceAnchor'] != anchor or
                frame['frameAnchor'] != [anchor[0]-left, anchor[1]-top]):
            raise ValueError('Contact registration differs from review.')
        if (crop[:12, :, 3].any() or crop[-12:, :, 3].any() or
                crop[:, :12, 3].any() or crop[:, -12:, 3].any()):
            raise ValueError('Transparent padding is contaminated or clipped.')
        expected[y:y+h, x:x+w] = crop
        cursor += w
    if not np.array_equal(expected, actual) or digest(actual.tobytes()) != review['runtimeRgbaSha256']:
        raise ValueError('Atlas is not the exact retained original RGBA.')
    if 'loop' in Image.open(HERE / 'anchored-breathe.gif').info:
        raise ValueError('Offline diagnostic must be nonlooping.')
    if announce:
        print('PASS: four original components; exact retained RGBA; twelve-pixel padding; one shared scale; agreed 2400 ms cycle; nonlooping diagnostic.')
    return actual


if __name__ == '__main__':
    actual = verify()
    altered = actual.copy()
    y, x = np.argwhere(altered[:, :, 3] > 128)[0]
    altered[y, x, 1] ^= 1
    try:
        verify(altered, announce=False)
    except ValueError as error:
        print(f'PASS: one-channel negative control rejected: {error}')
    else:
        raise AssertionError('Negative control was incorrectly accepted.')
