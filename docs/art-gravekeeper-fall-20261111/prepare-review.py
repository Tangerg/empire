"""Extract original generated RGBA and render offline registration diagnostics.

Only masks of detached alpha<=8 noise are removed. Retained pixels are copied
byte-for-byte. Resampling, mirroring and fading occur in diagnostics only.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--reference-root', type=Path, required=True)
ROOT = parser.parse_args().reference_root.resolve()
SCALE = 0.1
ANCHORS = [(164, 562), (446, 562), (738, 562), (1023, 553), (1411, 553), (1832, 553)]
NAMES = ['fall-buckle', 'fall-kneel', 'fall-collapse', 'fall-contact', 'fall-settle', 'fall-still']
SPANS = [60, 70, 90, 110, 110, 180]
PADDING = 12


def sha(data):
    return hashlib.sha256(data).hexdigest()


source_path = HERE / 'source-row.png'
source = np.array(Image.open(source_path).convert('RGBA'))
labels, count = ndimage.label(source[:, :, 3] > 16)
sizes = np.bincount(labels.ravel())
ids = sorted((i for i in range(1, count + 1) if sizes[i] > 1000),
             key=lambda i: np.where(labels == i)[1].min())
assert len(ids) == 6
labels = np.where(np.isin(labels, ids), labels, 0)
distance, nearest = ndimage.distance_transform_edt(labels == 0, return_indices=True)
owner = labels[nearest[0], nearest[1]]
keep = (distance <= 3) & (source[:, :, 3] > 0)
removed = (source[:, :, 3] > 0) & ~keep
assert int(source[:, :, 3][removed].max()) <= 8
frames, crops = [], []
x_cursor = 0
for index, (ident, anchor) in enumerate(zip(ids, ANCHORS)):
    mask = keep & (owner == ident)
    ys, xs = np.where(mask)
    left, top = int(xs.min()) - PADDING, int(ys.min()) - PADDING
    right, bottom = int(xs.max()) + 1 + PADDING, int(ys.max()) + 1 + PADDING
    assert left >= 0 and top >= 0 and right <= source.shape[1] and bottom <= source.shape[0]
    crop = source[top:bottom, left:right].copy()
    crop[~mask[top:bottom, left:right]] = 0
    height, width = crop.shape[:2]
    frame = {
        'sourceComponent': index,
        'frameName': NAMES[index],
        'sourceBounds': [left, top, right, bottom],
        'frameRegion': [x_cursor, 0, width, height],
        'sourceAnchor': list(anchor),
        'frameAnchor': [anchor[0] - left, anchor[1] - top],
        'retainedPixels': int(mask.sum()),
        'pixelSha256': sha(crop.tobytes()),
    }
    frames.append(frame)
    crops.append(crop)
    Image.fromarray(crop).save(HERE / f'pose-{index}-{NAMES[index]}.png')
    x_cursor += width
atlas = np.zeros((max(c.shape[0] for c in crops), x_cursor, 4), dtype=np.uint8)
for frame, crop in zip(frames, crops):
    x, y, w, h = frame['frameRegion']
    atlas[y:y+h, x:x+w] = crop
atlas_path = HERE / 'gravekeeper-fall-v1.png'
Image.fromarray(atlas).save(atlas_path)
references = [
    'godot/assets/painted-units-v1/unit-gravekeeper-gait-v2.png',
    'godot/assets/painted-actions-v1/gravekeeper-breathe-v1.png',
    'godot/assets/painted-actions-v1/gravekeeper-cast-v1.png',
    'godot/assets/painted-actions-v1/gravekeeper-recoil-v1.png',
]
review = {
    'status': 'proposed-art-only-awaiting-independent-and-native-review',
    'generator': 'built-in image_gen',
    'source': source_path.name,
    'sourceSha256': sha(source_path.read_bytes()),
    'sourceRgbaSha256': sha(source.tobytes()),
    'sourceDimensions': [source.shape[1], source.shape[0]],
    'prompt': 'prompt-v1.txt',
    'promptSha256': sha((HERE / 'prompt-v1.txt').read_bytes()),
    'references': [{'path': p, 'sha256': sha((ROOT / p).read_bytes())} for p in references],
    'significantComponentAlphaThresholdExclusive': 16,
    'significantComponentPixelCountExclusive': 1000,
    'retainedAlphaFringeRadius': 3,
    'transparentPaddingPixels': PADDING,
    'removedPixels': int(removed.sum()),
    'maxRemovedAlpha': int(source[:, :, 3][removed].max()) if removed.any() else 0,
    'removedAlphaSum': int(source[:, :, 3][removed].sum()),
    'removedAlphaHistogram': {str(a): int(n) for a, n in zip(*np.unique(source[:, :, 3][removed], return_counts=True))},
    'originalNonzeroAlphaPixels': int((source[:, :, 3] > 0).sum()),
    'retainedPixels': int(keep.sum()),
    'runtimePng': atlas_path.name,
    'runtimePngSha256': sha(atlas_path.read_bytes()),
    'runtimeRgbaSha256': sha(atlas.tobytes()),
    'runtimeDimensions': [atlas.shape[1], atlas.shape[0]],
    'presentationScale': SCALE,
    'frameOrder': NAMES,
    'playbackFrames': list(range(6)),
    'playbackSpansMs': SPANS,
    'durationMs': sum(SPANS),
    'death': {'mode': 'after-clip', 'fadeMs': 160},
    'anchorPolicy': 'Anatomical support registration: first three shoe/knee contacts at source y562; final three side/hip contacts at y553. Foreground shovel projects below the body support line, consistent with three-quarter ground-plane depth. One shared scale, no per-frame size fit.',
    'frames': frames,
}
(HERE / 'extraction-review.json').write_text(json.dumps(review, indent=2) + '\n')

manifest = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
old = next(a for a in manifest['assets'] if a.get('assetId') == 'C01-FFV1-UNIT-GRAVEKEEPER')
idle = next(a for a in manifest['assets'] if a.get('id') == 'gravekeeper-breathe-v1')
recoil = next(a for a in manifest['assets'] if a.get('id') == 'gravekeeper-recoil-v1')


def reference(asset, index, name):
    x, y, w, h = asset['frameRegions'][index]
    path = ROOT / 'godot' / asset['resourcePath'].removeprefix('res://')
    im = Image.open(path).convert('RGBA').crop((x, y, x+w, y+h))
    return name, im, asset['frameAnchors'][index], asset['presentationScale']


items = [reference(old, 0, 'gait idle'), reference(idle, 0, 'breathe rest'),
         reference(idle, 3, 'breathe exhale'), reference(recoil, 0, 'recoil onset'),
         reference(recoil, 1, 'recoil peak'), reference(recoil, 3, 'recoil ready')]
items += [(NAMES[i], Image.fromarray(crop), frames[i]['frameAnchor'], SCALE)
          for i, crop in enumerate(crops)]


def render_pose(item, mirrored, size=(90, 76), target=(35, 53)):
    _, im, anchor, scale = item
    sx = -scale if mirrored else scale
    # Mirror about the same logical actor anchor, never the crop midpoint.
    tx = 55 if mirrored else target[0]
    coeff = (1/sx, 0, anchor[0] - tx/sx,
             0, 1/scale, anchor[1] - target[1]/scale)
    return im.transform(size, Image.Transform.AFFINE, coeff, Image.Resampling.BILINEAR)


contact = Image.new('RGB', (90 * len(items), 160), '#28333c')
draw = ImageDraw.Draw(contact)
for facing in range(2):
    for i, item in enumerate(items):
        x, y = i*90, facing*80
        tile = render_pose(item, bool(facing))
        draw.line((x+4, y+53, x+86, y+53), fill='#46535b')
        contact.paste(tile, (x, y), tile)
        draw.text((x+3, y+64), item[0].replace('fall-', ''), fill='#e6edf3')
contact.save(HERE / 'native-size-contact.png')
for multiplier in [3, 6]:
    contact.resize((contact.width * multiplier, contact.height * multiplier), Image.Resampling.NEAREST).save(HERE / f'native-size-contact-{multiplier}x.png')

# Each diagnostic shows two complete nonlooping death paths. Neither simulates gameplay.
sequence = []
for handoff in ['idle', 'recoil']:
    sequence += [(1, 500, 1.0, handoff + ' handoff')]
    if handoff == 'recoil':
        sequence += [(3, 40, 1.0, 'recoil onset'), (4, 110, 1.0, 'recoil peak')]
    sequence += [(i+6, SPANS[i], 1.0, NAMES[i]) for i in range(6)]
    sequence += [(11, 40, alpha, f'fade {j+1}/4') for j, alpha in enumerate([0.75, 0.5, 0.25, 0.0])]
    sequence += [(11, 300, 0.0, 'removed')]
diagnostic = []
preview_dir = HERE / 'diagnostic-frames'
preview_dir.mkdir(exist_ok=True)
for index, (item_index, duration, alpha, label) in enumerate(sequence):
    item = items[item_index]
    im = Image.new('RGB', (200, 90), '#28333c')
    d = ImageDraw.Draw(im)
    for facing in range(2):
        tile = render_pose(item, bool(facing))
        if alpha != 1.0:
            tile.putalpha(tile.getchannel('A').point(lambda p: round(p * alpha)))
        d.line((facing*100+9, 53, facing*100+91, 53), fill='#46535b')
        im.paste(tile, (facing*100+5, 0), tile)
    d.text((5, 64), label + f' ({duration} ms)', fill='#e6edf3')
    d.text((5, 77), 'offline / right + left', fill='#b8c2cc')
    frame = im.resize((600, 270), Image.Resampling.NEAREST)
    diagnostic.append(frame)
    frame.save(preview_dir / f'{index:02}.png')
diagnostic[0].save(HERE / 'anchored-fall.gif', save_all=True,
                   append_images=diagnostic[1:], duration=[s[1] for s in sequence],
                   disposal=2, optimize=False)
(HERE / 'diagnostic-sequence.json').write_text(json.dumps({
    'description': 'Offline nonlooping right/left registration diagnostic, not captured gameplay. Two paths: idle to fall, then recoil to fall, each with 180 ms fully grounded hold and subsequent 160 ms fade.',
    'resampling': 'Single uniform affine scale with bilinear filtering; 3x nearest magnification. Mirroring and alpha fade affect diagnostics only.',
    'sequence': [{'item': items[i][0], 'spanMs': span, 'alpha': alpha, 'label': label, 'file': f'diagnostic-frames/{j:02}.png'}
                 for j, (i, span, alpha, label) in enumerate(sequence)],
    'totalMs': sum(s for _, s, _, _ in sequence),
}, indent=2) + '\n')
print(json.dumps(review, indent=2))
