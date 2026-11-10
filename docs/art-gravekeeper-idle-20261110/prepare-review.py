"""Extract generated RGBA components and render offline registration diagnostics.

This script does not change source pixels or production files. Resampling occurs
only in explicitly labelled review images, never in the proposed runtime atlas.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SCALE = 0.0792
ANCHORS = [(303, 598), (845, 598), (1387, 598), (1929, 598)]
NAMES = ['breathe-rest', 'breathe-inhale', 'breathe-high', 'breathe-exhale']
SPANS = [700, 500, 500, 700]
PADDING = 12


def sha(data):
    return hashlib.sha256(data).hexdigest()


source_path = HERE / 'source-row.png'
source = np.array(Image.open(source_path).convert('RGBA'))
labels, count = ndimage.label(source[:, :, 3] > 16)
sizes = np.bincount(labels.ravel())
ids = sorted((i for i in range(1, count + 1) if sizes[i] > 1000),
             key=lambda i: np.where(labels == i)[1].min())
assert len(ids) == 4
labels = np.where(np.isin(labels, ids), labels, 0)
distance, nearest = ndimage.distance_transform_edt(labels == 0, return_indices=True)
owner = labels[nearest[0], nearest[1]]
keep = (distance <= 3) & (source[:, :, 3] > 0)
removed = (source[:, :, 3] > 0) & ~keep
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
atlas_path = HERE / 'gravekeeper-breathe-v1.png'
Image.fromarray(atlas).save(atlas_path)
references = [
    'godot/assets/painted-units-v1/unit-gravekeeper-gait-v2.png',
    'godot/assets/painted-actions-v1/gravekeeper-cast-v1.png',
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
    'originalNonzeroAlphaPixels': int((source[:, :, 3] > 0).sum()),
    'retainedPixels': int(keep.sum()),
    'runtimePng': atlas_path.name,
    'runtimePngSha256': sha(atlas_path.read_bytes()),
    'runtimeRgbaSha256': sha(atlas.tobytes()),
    'runtimeDimensions': [atlas.shape[1], atlas.shape[0]],
    'presentationScale': SCALE,
    'frameOrder': NAMES,
    'playbackFrames': [0, 1, 2, 3],
    'playbackSpansMs': SPANS,
    'durationMs': sum(SPANS),
    'frames': frames,
}
(HERE / 'extraction-review.json').write_text(json.dumps(review, indent=2) + '\n')

manifest = json.loads((ROOT / 'godot/data/selected-assets.json').read_text())
old = next(a for a in manifest['assets'] if a.get('assetId') == 'C01-FFV1-UNIT-GRAVEKEEPER')
cast = next(a for a in manifest['assets'] if a.get('id') == 'gravekeeper-cast-v1')


def reference(asset, index, name):
    x, y, w, h = asset['frameRegions'][index]
    path = ROOT / 'godot' / asset['resourcePath'].removeprefix('res://')
    im = Image.open(path).convert('RGBA').crop((x, y, x+w, y+h))
    return name, im, asset['frameAnchors'][index], asset['presentationScale']


items = [reference(old, 0, 'old idle')]
items += [(NAMES[i], Image.fromarray(crop), frames[i]['frameAnchor'], SCALE)
          for i, crop in enumerate(crops)]
items += [reference(cast, 0, 'cast ready'), reference(cast, 5, 'cast rest')]


def render_pose(item, mirrored, size=(80, 72), target=(40, 52)):
    _, im, anchor, scale = item
    # One affine display scale, with registration to the declared support anchor.
    # Negative x is the normal mirrored-facing display, not a newly authored pose.
    sx = -scale if mirrored else scale
    coeff = (1/sx, 0, anchor[0] - target[0]/sx,
             0, 1/scale, anchor[1] - target[1]/scale)
    return im.transform(size, Image.Transform.AFFINE, coeff, Image.Resampling.BILINEAR)


contact = Image.new('RGB', (80 * len(items), 160), '#28333c')
draw = ImageDraw.Draw(contact)
for facing in range(2):
    for i, item in enumerate(items):
        x, y = i*80, facing*80
        tile = render_pose(item, bool(facing))
        contact.paste(tile, (x, y), tile)
        draw.line((x+4, y+52, x+76, y+52), fill='#46535b')
        draw.text((x+3, y+62), item[0].replace('breathe-', ''), fill='#e6edf3')
contact.save(HERE / 'native-size-contact.png')
contact.resize((contact.width * 3, contact.height * 3), Image.Resampling.NEAREST).save(HERE / 'native-size-contact-3x.png')

# Nonlooping diagnostic: old idle -> two complete breaths -> old idle ->
# accepted cast ready/rest -> new rest. This is not gameplay or a casting clip.
sequence = [(0, 700)]
sequence += [(i+1, SPANS[i]) for _ in range(2) for i in range(4)]
sequence += [(0, 500), (5, 500), (6, 500), (1, 700)]
diagnostic = []
for index, duration in sequence:
    item = items[index]
    im = Image.new('RGB', (180, 88), '#28333c')
    d = ImageDraw.Draw(im)
    for facing in range(2):
        tile = render_pose(item, bool(facing))
        im.paste(tile, (facing*90+5, 0), tile)
        d.line((facing*90+9, 52, facing*90+81, 52), fill='#46535b')
    d.text((5, 62), item[0] + f' ({duration} ms)', fill='#e6edf3')
    d.text((5, 75), 'offline / right + left', fill='#b8c2cc')
    diagnostic.append(im.resize((540, 264), Image.Resampling.NEAREST))
diagnostic[0].save(HERE / 'anchored-breathe.gif', save_all=True,
                   append_images=diagnostic[1:], duration=[s[1] for s in sequence],
                   disposal=2, optimize=False)
# Also retain each diagnostic frame without GIF palette reduction.
preview_dir = HERE / 'diagnostic-frames'
preview_dir.mkdir(exist_ok=True)
for index, im in enumerate(diagnostic):
    im.save(preview_dir / f'{index:02}.png')
(HERE / 'diagnostic-sequence.json').write_text(json.dumps({
    'description': 'Offline, nonlooping registration diagnostic; not captured gameplay.',
    'resampling': 'Single exact uniform affine scale with bilinear filtering; 3x nearest magnification.',
    'sequence': [{'item': items[i][0], 'spanMs': span, 'file': f'diagnostic-frames/{j:02}.png'}
                 for j, (i, span) in enumerate(sequence)],
    'totalMs': sum(s for _, s in sequence),
}, indent=2) + '\n')
print(json.dumps(review, indent=2))
