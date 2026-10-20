from pathlib import Path
from PIL import Image, ImageDraw
import hashlib, json, numpy as np
from scipy.ndimage import distance_transform_edt

B = Path(__file__).parent
POSES = ['idle', 'walk-a', 'carry-or-work', 'walk-b']
SCALE = 44 / 926
CANONICAL = [834.5, 971]
ORIGIN = [8, 4]
FRAME = [104, 64]
SOLE_RANGES = {'idle': [(600, 800), (800, 1100)], 'walk-a': [(520, 790), (850, 1150)], 'carry-or-work': [(580, 800), (800, 1100)], 'walk-b': [(520, 760), (800, 1100)]}
COLORS = {'black': (0, 0, 0), 'white': (255, 255, 255), 'scene': (109, 126, 93)}
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def bounds(a, threshold):
    y, x = np.where(a > threshold)
    return [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)] if len(x) else None

images = {p: Image.open(B / p / 'master.png') for p in POSES}
common = [max(im.width for im in images.values()), max(im.height for im in images.values())]
rows = {}
for pose, im in images.items():
    d = B / pose
    a = np.array(im)[:, :, 3]
    perimeter = np.concatenate([a[0, :], a[-1, :], a[:, 0], a[:, -1]])
    feet = []
    for x0, x1 in SOLE_RANGES[pose]:
        yy, xx = np.where(a[800:, x0:x1] > 127)
        bottom = int(yy.max() + 800)
        yy, xx = np.where(a[bottom-3:bottom+1, x0:x1] > 127)
        feet.append({'xRange': [x0, x1], 'bottom': bottom, 'bottom4RowsBounds': [int(xx.min()+x0), bottom-3, int(xx.max()+x0+1), bottom+1], 'soleCenterX': float((xx.min()+xx.max())/2+x0)})
    foot = [sum(f['soleCenterX'] for f in feet)/2, max(f['bottom'] for f in feet)]
    delta = [(CANONICAL[i]-foot[i])*SCALE for i in range(2)]
    distance = distance_transform_edt(a < 128)
    qa = {'nativeSize': list(im.size), 'mode': im.mode, 'rawSha256': sha(d/'raw.png'), 'masterSha256': sha(d/'master.png'), 'rawMasterIdentical': sha(d/'raw.png') == sha(d/'master.png'), 'decodedPixelsSha256': hashlib.sha256(im.tobytes()).hexdigest(), 'bboxAlphaNonzero': bounds(a, 0), 'bboxAlphaGreaterThan1': bounds(a, 1), 'bboxAlphaGreaterThan127': bounds(a, 127), 'perimeterMaxAlpha': int(perimeter.max()), 'perimeterCountAlphaGreaterThan1': int((perimeter>1).sum()), 'alphaCountsAboveThreshold': {str(t): int((a>t).sum()) for t in [0, 1, 16, 64, 127, 240, 250, 254]}, 'maxAlphaMoreThan3pxFromOpaqueBody': int(a[distance>3].max()), 'measuredBootSoleBands': feet, 'sourceFoot': foot, 'sourcePolicy': 'Native raw and master unchanged; no source cleanup, paint, crop, alpha editing, per-pose scaling, or source padding. Separate composites and whole-canvas technical QA only.'}
    (d/'alpha-qa.json').write_text(json.dumps(qa, indent=2)+'\n')
    request = json.loads((d/'request.json').read_text())
    provenance = {'tool': 'built-in image_gen.imagegen', 'promptFileSha256': sha(d/'prompt.txt'), 'requestFileSha256': sha(d/'request.json'), 'references': [{'path': p, 'sha256': sha(Path(p))} for p in request['referenced_image_paths']], 'rawSha256': qa['rawSha256'], 'masterSha256': qa['masterSha256']}
    (d/'request-provenance.json').write_text(json.dumps(provenance, indent=2)+'\n')
    visible = qa['bboxAlphaGreaterThan127']
    full = [ORIGIN[0]+delta[0], ORIGIN[1]+delta[1], ORIGIN[0]+delta[0]+common[0]*SCALE, ORIGIN[1]+delta[1]+common[1]*SCALE]
    rows[pose] = {'nativeSize': list(im.size), 'padRight': common[0]-im.width, 'padBottom': common[1]-im.height, 'measuredBootSoleBands': feet, 'sourceSupportAnchor': foot, 'registrationOffsetLogical': delta, 'nativeVisibleBoundsAlphaGreaterThan127': visible, 'nativeAllNonzeroAlphaBounds': qa['bboxAlphaNonzero'], 'proposedLogicalVisibleBounds': [ORIGIN[j%2]+delta[j%2]+visible[j]*SCALE for j in range(4)], 'proposedLogicalFullCanvasBounds': full, 'wholeCanvasFitsProposedFrame': full[0]>=0 and full[1]>=0 and full[2]<=FRAME[0] and full[3]<=FRAME[1], 'perimeterGate': {'maxNativePerimeterAlpha': qa['perimeterMaxAlpha'], 'pixelsWithPerimeterAlphaGreaterThan1': qa['perimeterCountAlphaGreaterThan1'], 'passesNativeSilhouettePerimeterGate': qa['perimeterMaxAlpha']<=1}}
    for name, color in COLORS.items():
        canvas = Image.new('RGBA', im.size, color+(255,))
        canvas.alpha_composite(im)
        canvas.convert('RGB').save(d/f'qa-{name}.png')
    annotated = Image.open(d/'qa-white.png')
    draw = ImageDraw.Draw(annotated)
    for f in feet:
        box = f['bottom4RowsBounds']
        draw.rectangle((box[0], box[1]-3, box[2], box[3]+3), outline=(255,0,128), width=3)
        draw.text((box[0], box[1]-22), f"bottom={f['bottom']} centerX={f['soleCenterX']}", fill=(255,0,128))
    annotated.save(d/'qa-foot-measurements.png')

registration = {'status': 'Measured native source quartet proposal; parent formal integration pending. No repository or runtime writes.', 'sharedScale': SCALE, 'bodyReferenceSourcePixels': 926, 'bodyTargetHeightLogicalPixels': 44, 'bodyMeasurement': 'Accepted idle hair crown alpha>127 y45 to supporting lowest sole y971 =926 pixels. One fixed44/926 scale for every whole source canvas.', 'canonicalSourceFoot': CANONICAL, 'commonSourceCanvas': common, 'paddingPolicy': 'Export-only transparent right/bottom padding to common canvas. Raw/master native dimensions and pixels remain unchanged.', 'canvasPaddingToleranceRequired': max(max(r['padRight'],r['padBottom']) for r in rows.values()), 'proposedCommonFrameSize': FRAME, 'proposedOrigin': ORIGIN, 'proposedAnchor': [ORIGIN[i]+CANONICAL[i]*SCALE for i in range(2)], 'registrationMethod': 'Independent alpha>127 bottom4-row boot sole bands; x average of centers, y lowest sole. Whole-canvas translation only; no per-pose fitting.', 'poses': rows, 'requiredMaximumTranslationLogicalPixels': max(abs(x) for r in rows.values() for x in r['registrationOffsetLogical'])}
(B/'registration-evidence.json').write_text(json.dumps(registration, indent=2)+'\n')
config = {'topicId': 'C01-MISSION-SCRIBE', 'frames': {p: str(B/p/'master.png') for p in POSES}, 'scale': SCALE, 'origin': ORIGIN, 'frameSize': FRAME, 'sourceFoot': CANONICAL, 'sourceFeet': {p:r['sourceSupportAnchor'] for p,r in rows.items()}, 'registrationOffsets': {p:r['registrationOffsetLogical'] for p,r in rows.items()}, 'registrationLimit': 2, 'canvasPaddingTolerance': 2}
(B/'qa-export-config.json').write_text(json.dumps(config, indent=2)+'\n')
print(json.dumps(registration, indent=2))
