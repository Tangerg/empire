"""Read-only source, RGBA extraction, registration and diagnostic verification."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
NAMES = ['fall-buckle', 'fall-kneel', 'fall-collapse', 'fall-contact', 'fall-settle', 'fall-still']
SPANS = [60, 70, 90, 110, 110, 180]
ANCHORS = [[164, 562], [446, 562], [738, 562], [1023, 553], [1411, 553], [1832, 553]]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def verify(runtime_pixels=None, reference_root=None, announce=True):
    review = json.loads((HERE / 'extraction-review.json').read_text())
    source_path = HERE / review['source']
    if digest(source_path.read_bytes()) != review['sourceSha256']:
        raise ValueError('Original generation PNG has changed.')
    prompt_path = HERE / review['prompt']
    if digest(prompt_path.read_bytes()) != review['promptSha256']:
        raise ValueError('Generation prompt has changed.')
    provenance = json.loads((HERE / 'generation-provenance.json').read_text())
    if provenance['prompt'] != prompt_path.read_text() or provenance['sourceSha256'] != review['sourceSha256']:
        raise ValueError('Generation provenance differs from preserved source or exact prompt.')
    if provenance['parameters']['transparent_background'] is not True:
        raise ValueError('Generation provenance must record real transparent output.')
    if reference_root:
        for ref in review['references']:
            if digest((reference_root / ref['path']).read_bytes()) != ref['sha256']:
                raise ValueError('Accepted reference changed: ' + ref['path'])
    source = np.array(Image.open(source_path).convert('RGBA'))
    if digest(source.tobytes()) != review['sourceRgbaSha256'] or list(source.shape[1::-1]) != review['sourceDimensions']:
        raise ValueError('Original generation RGBA or dimensions changed.')
    labels, count = ndimage.label(source[:, :, 3] > 16)
    sizes = np.bincount(labels.ravel())
    ids = sorted((i for i in range(1, count + 1) if sizes[i] > 1000),
                 key=lambda i: np.where(labels == i)[1].min())
    if len(ids) != 6:
        raise ValueError('Expected six independently drawn complete components.')
    labels = np.where(np.isin(labels, ids), labels, 0)
    distance, nearest = ndimage.distance_transform_edt(labels == 0, return_indices=True)
    owner = labels[nearest[0], nearest[1]]
    keep = (distance <= 3) & (source[:, :, 3] > 0)
    removed = (source[:, :, 3] > 0) & ~keep
    max_removed_alpha = int(source[:, :, 3][removed].max()) if removed.any() else 0
    histogram = {str(a): int(n) for a, n in zip(*np.unique(source[:, :, 3][removed], return_counts=True))}
    if (int(removed.sum()) != review['removedPixels'] or max_removed_alpha != review['maxRemovedAlpha'] or
            max_removed_alpha > 8 or int(source[:, :, 3][removed].sum()) != review['removedAlphaSum'] or
            histogram != review['removedAlphaHistogram']):
        raise ValueError('Removed pixels differ from reviewed detached alpha noise.')
    if ((source[:, :, 3] > 8) & ~keep).any():
        raise ValueError('A meaningful source pixel was discarded.')
    if int(keep.sum()) != review['retainedPixels'] or int((source[:, :, 3] > 0).sum()) != review['originalNonzeroAlphaPixels']:
        raise ValueError('Source pixel counts differ from review.')
    if (review['significantComponentAlphaThresholdExclusive'] != 16 or
            review['significantComponentPixelCountExclusive'] != 1000 or
            review['retainedAlphaFringeRadius'] != 3 or review['transparentPaddingPixels'] != 12):
        raise ValueError('Extraction parameters differ from review.')
    if (review['playbackFrames'] != list(range(6)) or review['frameOrder'] != NAMES or
            review['playbackSpansMs'] != SPANS or review['presentationScale'] != 0.1 or
            review['durationMs'] != 620 or review['death'] != {'mode': 'after-clip', 'fadeMs': 160}):
        raise ValueError('Timing, shared scale or post-clip fade differs from the agreed art contract.')
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
        if frame['sourceComponent'] != index or frame['frameName'] != NAMES[index]:
            raise ValueError('Every original component must remain in source order.')
        mask = keep & (owner == ident)
        ys, xs = np.where(mask)
        bounds = [int(xs.min())-12, int(ys.min())-12, int(xs.max())+13, int(ys.max())+13]
        if frame['sourceBounds'] != bounds:
            raise ValueError('Crop must preserve twelve transparent padding pixels.')
        left, top, right, bottom = bounds
        crop = source[top:bottom, left:right].copy()
        crop[~mask[top:bottom, left:right]] = 0
        pose = np.array(Image.open(HERE / f"pose-{index}-{frame['frameName']}.png").convert('RGBA'))
        if not np.array_equal(pose, crop) or digest(crop.tobytes()) != frame['pixelSha256']:
            raise ValueError('Individual pose differs from exact retained source RGBA.')
        if frame['retainedPixels'] != int(mask.sum()):
            raise ValueError('Retained component pixel count has changed.')
        x, y, w, h = frame['frameRegion']
        if [x, y, w, h] != [cursor, 0, right-left, bottom-top]:
            raise ValueError('Packed rectangle differs from retained source crop.')
        anchor = ANCHORS[index]
        if frame['sourceAnchor'] != anchor or frame['frameAnchor'] != [anchor[0]-left, anchor[1]-top]:
            raise ValueError('Anatomical support registration differs from review.')
        if (crop[:12, :, 3].any() or crop[-12:, :, 3].any() or crop[:, :12, 3].any() or crop[:, -12:, 3].any()):
            raise ValueError('Transparent crop padding is contaminated or clipped.')
        expected[y:y+h, x:x+w] = crop
        cursor += w
    if not np.array_equal(expected, actual) or digest(actual.tobytes()) != review['runtimeRgbaSha256']:
        raise ValueError('Atlas is not exact retained original RGBA.')
    animation = Image.open(HERE / 'anchored-fall.gif')
    if 'loop' in animation.info:
        raise ValueError('Offline diagnostic must be nonlooping.')
    sequence = json.loads((HERE / 'diagnostic-sequence.json').read_text())
    if animation.n_frames != len(sequence['sequence']):
        raise ValueError('Diagnostic must retain every declared stage.')
    if sequence['totalMs'] != sum(f['spanMs'] for f in sequence['sequence']):
        raise ValueError('Diagnostic duration is inconsistent.')
    for index, frame in enumerate(sequence['sequence']):
        animation.seek(index)
        if animation.info['duration'] != frame['spanMs'] or not (HERE / frame['file']).is_file():
            raise ValueError('GIF timing differs from lossless diagnostic sequence.')
    for start in [1, 15]:
        six = sequence['sequence'][start:start+6]
        if [f['item'] for f in six] != NAMES or [f['spanMs'] for f in six] != SPANS:
            raise ValueError('Diagnostic must contain complete finite 620 ms fall.')
        fade = sequence['sequence'][start+6:start+10]
        if [f['alpha'] for f in fade] != [0.75, 0.5, 0.25, 0.0] or sum(f['spanMs'] for f in fade) != 160:
            raise ValueError('Fade must follow the final grounded hold and last 160 ms.')
    if announce:
        print('PASS: six original components; exact retained RGBA; all alpha>8 preserved; twelve-pixel padding; uniform0.1 scale; 620 ms fall +160 ms after-clip fade; nonlooping both-facing idle/recoil diagnostic.')
    return actual


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path)
    args = parser.parse_args()
    actual = verify(reference_root=args.reference_root)
    altered = actual.copy()
    y, x = np.argwhere(altered[:, :, 3] > 128)[0]
    altered[y, x, 1] ^= 1
    try:
        verify(altered, args.reference_root, announce=False)
    except ValueError as error:
        print(f'PASS: one-channel mutation rejected: {error}')
    else:
        raise AssertionError('One-channel mutation was incorrectly accepted.')
