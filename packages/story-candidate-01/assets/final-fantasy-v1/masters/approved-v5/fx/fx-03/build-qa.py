from pathlib import Path
import json, hashlib
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT = Path(__file__).parent
OUT = ROOT / 'qa'
OUT.mkdir(exist_ok=True)
PHASES = ['approach', 'impact', 'burst', 'dissipate']
BGS = {'light': '#eee5cf', 'dark': '#202426', 'olive': '#687047'}
metrics = []
exports = {1: [], 2: []}
font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 14)

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

for phase in PHASES:
    p = ROOT / phase / 'master.png'
    im = Image.open(p).convert('RGBA')
    a = np.array(im.getchannel('A'))
    edge = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    row = {
        'phase': phase, 'sourceSize': list(im.size), 'mode': im.mode,
        'rawEqualsMaster': sha(p) == sha(ROOT / phase / 'raw.png'),
        'masterSha256': sha(p), 'outerEdgeAlphaMax': int(edge.max()),
        'outerEdgeNonzeroCount': int(np.count_nonzero(edge)),
        'alphaMass': float(a.sum() / 255),
        'bboxAlpha1': im.getchannel('A').getbbox(),
        'bboxAlpha128': im.getchannel('A').point(lambda x: 255 if x >= 128 else 0).getbbox(),
        'exports': {}
    }
    for scale in [1, 2]:
        # Shared whole-canvas transform only. Premultiplied-alpha Lanczos.
        fit = im.convert('RGBa').resize((28 * scale, 28 * scale), Image.Resampling.LANCZOS).convert('RGBA')
        cell = Image.new('RGBA', (32 * scale, 32 * scale), (0, 0, 0, 0))
        cell.paste(fit, (2 * scale, 2 * scale))
        cell.save(OUT / f'{phase}@{scale}x.png')
        exports[scale].append(cell)
        ea = np.array(cell.getchannel('A'))
        row['exports'][f'{scale}x'] = {
            'size': list(cell.size), 'bboxAlpha1': cell.getchannel('A').getbbox(),
            'alphaMass': float(ea.sum() / 255),
            'outerEdgeAlphaMax': int(max(ea[0].max(), ea[-1].max(), ea[:, 0].max(), ea[:, -1].max()))
        }
    metrics.append(row)

for scale, frames in exports.items():
    strip = Image.new('RGBA', (128 * scale, 32 * scale))
    for index, frame in enumerate(frames):
        strip.paste(frame, (index * 32 * scale, 0))
    strip.save(OUT / f'phase-strip@{scale}x.png')

# Labels are outside the native game cells; enlarged views are nearest-neighbor.
board = Image.new('RGB', (1230, 620), '#ddd9d0')
d = ImageDraw.Draw(board)
d.text((15, 12), 'FX03 fixed 32px cell. Each column: native 32 / native 64 / 32px enlarged 4x nearest. Offline QA only.', fill='black', font=font)
for bg_index, (bg_name, color) in enumerate(BGS.items()):
    y = 50 + bg_index * 185
    d.text((12, y), bg_name, fill='black', font=font)
    for phase_index, phase in enumerate(PHASES):
        x = 110 + phase_index * 275
        d.text((x, y), phase, fill='black', font=font)
        native = Image.new('RGBA', (32, 32), color)
        native.alpha_composite(exports[1][phase_index])
        board.paste(native.convert('RGB'), (x, y + 24))
        native2 = Image.new('RGBA', (64, 64), color)
        native2.alpha_composite(exports[2][phase_index])
        board.paste(native2.convert('RGB'), (x + 42, y + 24))
        zoom = native.resize((128, 128), Image.Resampling.NEAREST)
        board.paste(zoom.convert('RGB'), (x + 116, y + 23))
board.save(OUT / 'native-32-64-light-dark-olive.png')

# Lossless APNG uses millisecond timing. GIF preview necessarily quantizes to 10ms.
for scale, frames in exports.items():
    for bg_name, color in BGS.items():
        composited = []
        for frame in frames:
            bg = Image.new('RGBA', frame.size, color)
            bg.alpha_composite(frame)
            composited.append(bg)
        composited[0].save(OUT / f'12fps-finalhold-{bg_name}@{scale}x.png', save_all=True, append_images=composited[1:], duration=[83,84,83,170], loop=1, disposal=0, blend=0)
        large = [f.resize((256, 256), Image.Resampling.NEAREST).convert('RGB') for f in composited]
        large[0].save(OUT / f'12fps-finalhold-{bg_name}@{scale}x.gif', save_all=True, append_images=large[1:], duration=[80,90,80,170], loop=0, disposal=2)

timeline = Image.new('RGB', (1050, 310), '#ddd9d0')
d = ImageDraw.Draw(timeline)
times = [(0,0),(83.333,1),(166.667,2),(250,3),(333.333,3),(416.667,3)]
d.text((12,10), '12 fps sample sequence: 0 / 83 / 167 / 250 / 333 / 417ms. Last phase held through 420ms.', fill='black', font=font)
for i, (time, phase) in enumerate(times):
    x = 12 + i * 172
    d.text((x,35), f'{time:g}ms {PHASES[phase]}', fill='black', font=font)
    im = Image.new('RGBA', (32,32), BGS['olive'])
    im.alpha_composite(exports[1][phase])
    timeline.paste(im.resize((160,160),Image.Resampling.NEAREST).convert('RGB'),(x,65))
timeline.save(OUT/'timeline-finalhold-420ms.png')

report = {
    'topicId':'C01-FX-03', 'phaseOrder':PHASES,
    'sourceCanvas':[1254,1254], 'conceptualPivot':[627,627],
    'wholeCanvasFit':{'logicalSize':28,'logicalOffset':[2,2],'cell':[32,32]},
    'noPerPhaseFitCropOrTranslate': True,
    'estimatedApproachTip':[657,697], 'estimatedImpactPoint':[719,741],
    'estimatedImpactLogical':[2+719*28/1254,2+741*28/1254],
    'estimatedImpactOffsetFromLogicalPivot':[719*28/1254-14,741*28/1254-14],
    'fps':12, 'loop':False,'blendMode':'normal',
    'phaseStartMs':[0,1000/12,2000/12,250],'finalHoldUntilMs':420,
    'apngDurationsMs':[83,84,83,170], 'gifPreviewDurationsMs':[80,90,80,170],
    'actualBrowserReview':False, 'frames':metrics,
}
(OUT/'qa-metrics.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
