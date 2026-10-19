from pathlib import Path
from PIL import Image
import numpy as np
import json, hashlib, shutil, sys, os

b = Path(__file__).parent
pose, source, roi_json, review = sys.argv[1:]
d = b / pose / os.environ.get('ATTEMPT', 'attempt-01')
src = Path(source)
for name in ['raw.png', 'master.png']:
    shutil.copyfile(src, d / name)
im = Image.open(src).convert('RGBA')
a = np.array(im.getchannel('A'))
edge = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
boots = []
for r in json.loads(roi_json):
    x0, y0, x1, y1 = r
    ys, xs = np.where(a[y0:y1, x0:x1] > 127)
    y = int(ys.max() + y0)
    _, xs = np.where(a[max(0, y - 3):y + 1, x0:x1] > 127)
    boots.append({'roi': r, 'soleY': y, 'soleBandMeanX': float(xs.mean() + x0)})
mean = [float(np.mean([v['soleBandMeanX'] for v in boots])), float(np.mean([v['soleY'] for v in boots]))]
bb = lambda t: Image.fromarray((a > t).astype('uint8') * 255).getbbox()
q = {'nativeSource': str(src), 'size': im.size, 'mode': im.mode,
     'sha256': hashlib.sha256(src.read_bytes()).hexdigest(),
     'bboxAlphaNonzero': bb(0), 'bboxAlphaGreaterThan1': bb(1), 'bboxAlphaGreaterThan127': bb(127),
     'alphaZeroCount': int((a == 0).sum()), 'alphaOpaqueCount': int((a == 255).sum()),
     'alphaSemitransparentCount': int(((a > 0) & (a < 255)).sum()),
     'perimeterMaxAlpha': int(edge.max()), 'perimeterCountAlphaGreaterThan1': int((edge > 1).sum()),
     'boots': boots, 'sourceFootMidpoint': mean,
     'suggestedSourceFoot': [round(mean[0]), max(v['soleY'] for v in boots)],
     'semanticReview': review, 'nativeBytesPreserved': True}
(d / 'alpha-qa.json').write_text(json.dumps(q, indent=2) + '\n')
for name, color in [('qa-white.png', (255,255,255,255)), ('qa-black.png', (0,0,0,255))]:
    bg = Image.new('RGBA', im.size, color)
    bg.alpha_composite(im)
    bg.convert('RGB').save(d / name)
req = json.loads((d / 'request.json').read_text())
(d / 'prompt.txt').write_text(req['prompt'])
req['promptSha256'] = hashlib.sha256((d / 'prompt.txt').read_bytes()).hexdigest()
req['nativeOutput'] = str(src)
(d / 'request.json').write_text(json.dumps(req, indent=2) + '\n')
if os.environ.get('SELECTED', '1') == '1':
    for p in d.iterdir():
        if p.is_file(): shutil.copyfile(p, b / pose / p.name)
print(json.dumps(q, indent=2))
