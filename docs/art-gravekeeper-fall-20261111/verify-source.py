"""Independently verify accepted original pixels and offline registration.

Read-only inputs. Uses six separate radius-three Euclidean dilations, not the
artist's global distance-transform ownership implementation. Writes only the
optional report path. This is not native playback or performance verification.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

NAMES = ['fall-buckle', 'fall-kneel', 'fall-collapse', 'fall-contact', 'fall-settle', 'fall-still']
SPANS = [60, 70, 90, 110, 110, 180]
ANCHORS = [[164, 562], [446, 562], [738, 562], [1023, 553], [1411, 553], [1832, 553]]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def same(actual, expected, message):
    assert actual.shape == expected.shape and np.array_equal(actual, expected), message


def load_rgba(path):
    im = Image.open(path)
    assert im.mode == 'RGBA', f'{path.name}: not original RGBA'
    return np.array(im)


def verify(packet, root):
    meta = json.loads((packet / 'extraction-review.json').read_text())
    source_path = packet / meta['source']
    source = load_rgba(source_path)
    assert sha(source_path.read_bytes()) == meta['sourceSha256']
    assert sha(source.tobytes()) == meta['sourceRgbaSha256']
    assert [source.shape[1], source.shape[0]] == meta['sourceDimensions'] == [2172, 724]
    assert sha((packet / meta['prompt']).read_bytes()) == meta['promptSha256']
    for ref in meta['references']:
        assert sha((root / ref['path']).read_bytes()) == ref['sha256'], ref['path']
    assert len(meta['references']) == 4
    provenance = json.loads((packet / 'generation-provenance.json').read_text())
    assert provenance['prompt'] == (packet / meta['prompt']).read_text()
    assert provenance['sourceSha256'] == meta['sourceSha256']
    assert provenance['referenceChecksums'] == meta['references']
    assert provenance['parameters']['transparent_background'] is True
    original_output = Path(provenance['originalOutputPath'])
    original_output_verified = original_output.is_file()
    if original_output_verified:
        assert original_output.read_bytes() == source_path.read_bytes(), 'generated original changed before source preservation'
    assert meta['significantComponentAlphaThresholdExclusive'] == 16
    assert meta['significantComponentPixelCountExclusive'] == 1000
    assert meta['retainedAlphaFringeRadius'] == 3
    assert meta['transparentPaddingPixels'] == 12
    assert meta['presentationScale'] == 0.1
    assert meta['frameOrder'] == NAMES and meta['playbackFrames'] == list(range(6))
    assert meta['playbackSpansMs'] == SPANS and meta['durationMs'] == sum(SPANS) == 620
    assert meta['death'] == {'mode': 'after-clip', 'fadeMs': 160}
    assert [f['sourceAnchor'] for f in meta['frames']] == ANCHORS
    assert len(meta['frames']) == 6

    labels, count = ndimage.label(source[:, :, 3] > 16)
    sizes = np.bincount(labels.ravel())
    ids = sorted([i for i in range(1, count + 1) if sizes[i] > 1000],
                 key=lambda i: np.where(labels == i)[1].min())
    assert len(ids) == 6
    yy, xx = np.ogrid[-3:4, -3:4]
    disk = xx * xx + yy * yy <= 9
    retained = np.zeros(source.shape[:2], dtype=bool)
    crops, records = [], []
    cursor = 0
    for index, (component, frame) in enumerate(zip(ids, meta['frames'])):
        mask = ndimage.binary_dilation(labels == component, structure=disk) & (source[:, :, 3] > 0)
        assert not np.any(retained & mask), 'ambiguous component ownership'
        retained |= mask
        ys, xs = np.where(mask)
        raw = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
        left, top, right, bottom = raw[0] - 12, raw[1] - 12, raw[2] + 12, raw[3] + 12
        assert frame['sourceBounds'] == [left, top, right, bottom]
        assert left >= 0 and top >= 0 and right <= source.shape[1] and bottom <= source.shape[0]
        crop = np.zeros((bottom - top, right - left, 4), dtype=np.uint8)
        local_mask = mask[top:bottom, left:right]
        crop[local_mask] = source[top:bottom, left:right][local_mask]
        assert frame['sourceComponent'] == index and frame['frameName'] == NAMES[index]
        assert frame['frameRegion'] == [cursor, 0, right - left, bottom - top]
        assert frame['frameAnchor'] == [ANCHORS[index][0] - left, ANCHORS[index][1] - top]
        assert frame['retainedPixels'] == int(mask.sum())
        assert frame['pixelSha256'] == sha(crop.tobytes())
        same(load_rgba(packet / f'pose-{index}-{NAMES[index]}.png'), crop, 'standalone pose changed source pixels')
        crops.append(crop)
        records.append({'name': NAMES[index], 'rawBounds': raw, 'sourceBounds': frame['sourceBounds'],
                        'frameRegion': frame['frameRegion'], 'sourceAnchor': ANCHORS[index],
                        'frameAnchor': frame['frameAnchor'], 'retainedPixels': int(mask.sum()),
                        'originalPixelsSha256': sha(crop.tobytes()),
                        'nativeNonzeroAlphaExtentsFromAnchor': [round((raw[0]-ANCHORS[index][0])*0.1, 4),
                                                              round((raw[1]-ANCHORS[index][1])*0.1, 4),
                                                              round((raw[2]-ANCHORS[index][0])*0.1, 4),
                                                              round((raw[3]-ANCHORS[index][1])*0.1, 4)]})
        cursor += crop.shape[1]
    removed = (source[:, :, 3] > 0) & ~retained
    assert not np.any((source[:, :, 3] > 8) & ~retained), 'discarded alpha above accepted bound'
    assert meta['removedPixels'] == int(removed.sum()) == 187405
    assert meta['retainedPixels'] == int(retained.sum()) == 341766
    assert meta['maxRemovedAlpha'] == int(source[:, :, 3][removed].max()) == 8
    assert meta['originalNonzeroAlphaPixels'] == int((source[:, :, 3] > 0).sum()) == 529171
    assert meta['removedAlphaSum'] == int(source[:, :, 3][removed].sum())
    hist = {str(a): int(n) for a, n in zip(*np.unique(source[:, :, 3][removed], return_counts=True))}
    assert meta['removedAlphaHistogram'] == hist
    expected = np.zeros((max(c.shape[0] for c in crops), cursor, 4), dtype=np.uint8)
    for frame, crop in zip(meta['frames'], crops):
        x, y, width, height = frame['frameRegion']
        expected[y:y+height, x:x+width] = crop
    runtime_path = packet / meta['runtimePng']
    runtime = load_rgba(runtime_path)
    same(runtime, expected, 'atlas is not exact original source reconstruction')
    assert [runtime.shape[1], runtime.shape[0]] == meta['runtimeDimensions'] == [2154, 405]
    assert sha(runtime_path.read_bytes()) == meta['runtimePngSha256']
    assert sha(runtime.tobytes()) == meta['runtimeRgbaSha256']

    # Test the pixel equality guard independently of file hashes.
    mutated = expected.copy()
    y, x = np.argwhere(mutated[:, :, 3] > 240)[0]
    mutated[y, x, 0] ^= 1
    try:
        same(mutated, expected, 'visible-pixel mutation')
    except AssertionError:
        mutation_rejected = True
    else:
        raise AssertionError('visible-pixel mutation incorrectly accepted')

    manifest = json.loads((root / 'godot/data/selected-assets.json').read_text())['assets']
    refs = {}
    for asset_id, indices in [('gravekeeper-breathe-v1', [(0, 'breathe rest')]),
                              ('gravekeeper-recoil-v1', [(0, 'recoil onset'), (1, 'recoil peak')])]:
        asset = next(a for a in manifest if a.get('id') == asset_id)
        image = Image.open(root / 'godot' / asset['resourcePath'].removeprefix('res://')).convert('RGBA')
        for i, name in indices:
            x, y, w, h = asset['frameRegions'][i]
            refs[name] = (image.crop((x, y, x+w, y+h)), asset['frameAnchors'][i], asset['presentationScale'])
    for i, name in enumerate(NAMES):
        refs[name] = (Image.fromarray(crops[i]), meta['frames'][i]['frameAnchor'], 0.1)
    diagnostic = json.loads((packet / 'diagnostic-sequence.json').read_text())
    sequence = diagnostic['sequence']
    assert len(sequence) == 26 and sum(s['spanMs'] for s in sequence) == diagnostic['totalMs'] == 3310
    for start in (1, 15):
        assert [s['item'] for s in sequence[start:start+6]] == NAMES
        assert [s['spanMs'] for s in sequence[start:start+6]] == SPANS
        assert [s['alpha'] for s in sequence[start:start+6]] == [1.0]*6
        assert [s['item'] for s in sequence[start+6:start+10]] == ['fall-still']*4
        assert [s['alpha'] for s in sequence[start+6:start+10]] == [.75, .5, .25, 0]
        assert [s['spanMs'] for s in sequence[start+6:start+10]] == [40]*4
    # Independently reconstruct the full image area; label text is outside this ROI.
    for row in sequence:
        sprite, anchor, scale = refs[row['item']]
        canvas = Image.new('RGB', (200, 60), '#28333c')
        d = ImageDraw.Draw(canvas)
        for facing in range(2):
            origin_x = facing*100 + 5
            target_x = 35 if facing == 0 else 55
            horizontal_scale = scale if facing == 0 else -scale
            coeff = (1/horizontal_scale, 0, anchor[0]-target_x/horizontal_scale,
                     0, 1/scale, anchor[1]-53/scale)
            rendered = sprite.transform((90, 60), Image.Transform.AFFINE, coeff, Image.Resampling.BILINEAR)
            rendered.putalpha(rendered.getchannel('A').point(lambda a: round(a*row['alpha'])))
            d.line((facing*100+9, 53, facing*100+91, 53), fill='#46535b')
            canvas.paste(rendered, (origin_x, 0), rendered)
        target = canvas.resize((600, 180), Image.Resampling.NEAREST)
        actual = Image.open(packet / row['file']).convert('RGB')
        assert actual.size == (600, 270)
        same(np.array(actual.crop((0, 0, 600, 180))), np.array(target), f"diagnostic registration: {row['file']}")
    contact = Image.open(packet / 'native-size-contact.png').convert('RGB')
    assert contact.size == (1080, 160)
    for n in (3, 6):
        same(np.array(Image.open(packet / f'native-size-contact-{n}x.png').convert('RGB')),
             np.array(contact.resize((1080*n, 160*n), Image.Resampling.NEAREST)), 'contact magnification changed pixels')
    gif = Image.open(packet / 'anchored-fall.gif')
    assert gif.n_frames == 26 and 'loop' not in gif.info
    gif_spans = []
    for i, row in enumerate(sequence):
        gif.seek(i)
        gif_spans.append(gif.info['duration'])
        assert gif.info['duration'] == row['spanMs']
    return {'status': 'PASS-source-and-offline-registration-only', 'sourceSha256': meta['sourceSha256'],
            'runtimePngSha256': meta['runtimePngSha256'], 'runtimeRgbaSha256': meta['runtimeRgbaSha256'],
            'sourceDimensions': meta['sourceDimensions'], 'runtimeDimensions': meta['runtimeDimensions'],
            'preservedSourceMatchesAvailableOriginalGeneration': original_output_verified,
            'exactPromptAndReferenceProvenance': True, 'allOriginalAlphaAbove8Retained': True, 'retainedOriginalRgbaPixels': int(retained.sum()),
            'removedPixels': int(removed.sum()), 'maxRemovedAlpha': 8, 'removedAlphaHistogram': hist,
            'allCropsAndAtlasExact': True, 'paddingEachSidePixels': 12, 'presentationScale': .1,
            'sixDistinctCropHashes': len(set(r['originalPixelsSha256'] for r in records)) == 6,
            'visibleOneChannelMutationRejected': mutation_rejected,
            'offlineDiagnosticImageAreasExactlyReconstructed': len(sequence),
            'nonloopingGifFrames': gif.n_frames, 'gifTotalMs': sum(gif_spans),
            'fallSpansMs': SPANS, 'clipMs': 620, 'fadeMs': 160,
            'fadeUsesUnchangedStillPoseGeometry': True, 'frames': records,
            'excluded': ['production integration', 'native playback', 'legal gameplay', 'performance']}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packet', type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument('--reference-root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    result = verify(args.packet.resolve(), args.reference_root.resolve())
    data = json.dumps(result, indent=2) + '\n'
    if args.report:
        args.report.write_text(data)
    print(data)
