#!/usr/bin/env python3
"""Non-destructive FX-02 artwork previews, not a registered exporter run."""
import hashlib, json, struct, sys, zlib
from pathlib import Path
sys.dont_write_bytecode = True
from PIL import Image, ImageDraw, ImageFont
import numpy as np

OUT = Path(__file__).resolve().parent
BASE = OUT.parent.parent
ORDER = ['onset', 'sweep', 'peak', 'dissipate']
BGS = {'light':'#f1e8d3','dark':'#292725','olive':'#626b43','white':'#ffffff','black':'#000000'}
REPO = Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
sys.path.insert(0, str(REPO/'tools/art'))
from export_fx_overrides import validate_fx_alpha

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def metric(im):
    a = np.asarray(im.getchannel('A'))
    ys, xs = np.indices(a.shape)
    mass = int(a.sum())
    box = lambda threshold: im.getchannel('A').point(lambda v:255 if v>threshold else 0).getbbox()
    return {**validate_fx_alpha(im, 'FX-02 QA'), 'size':list(im.size), 'mode':im.mode,
        'pixelSha256':hashlib.sha256(im.tobytes()).hexdigest(),
        'alphaLevels':len(np.unique(a)), 'alphaEquivalentPixels':round(mass/255,6),
        'nearOpaquePixels':int((a>=240).sum()), 'partialAlphaPixels':int(((a>0)&(a<255)).sum()),
        'alphaGt1BoundingBox':box(1), 'alphaGt16BoundingBox':box(16),
        'alphaWeightedCentroid':[round(float((xs*a).sum()/mass),5),round(float((ys*a).sum()/mass),5)]}

before = {str(p.relative_to(BASE)):sha(p) for phase in ORDER for p in (BASE/phase).iterdir() if p.is_file()}
sources, source_metrics = [], []
for phase in ORDER:
    raw = BASE/phase/'raw.png'
    master = BASE/phase/'master.png'
    im = Image.open(master)
    assert im.format=='PNG' and im.mode=='RGBA' and im.size==(1254,1254)
    assert raw.read_bytes()==master.read_bytes()
    a = np.array(im.getchannel('A'))
    central = a[:,600:655]
    tip = {}
    for threshold in [1,16,64,128]:
        pts = np.argwhere(central>threshold)
        tip[str(threshold)] = int(pts[:,0].max()) if len(pts) else None
    source_metrics.append({'phase':phase,'rawSha256':sha(raw),'masterSha256':sha(master),
        'rawMasterByteIdentical':True, 'centralNeedleBottomYByAlphaThreshold':tip, **metric(im)})
    sources.append(im)

frames = {}
output_metrics = {}
for density in [1,2]:
    side = 32*density
    sheet = Image.new('RGBA',(128*density,side))
    frames[density] = []
    for index,(phase,im) in enumerate(zip(ORDER,sources)):
        rendered = im.resize((28*density,28*density),Image.Resampling.LANCZOS)
        cell = Image.new('RGBA',(side,side))
        cell.paste(rendered,(2*density,2*density))
        sheet.paste(cell,(index*side,0))
        cell.save(OUT/f'{phase}@{density}x.png')
        frames[density].append(cell)
    sheet.save(OUT/f'fx-02@{density}x.png')
    output_metrics[str(density)] = [{'phase':phase,**metric(im)} for phase,im in zip(ORDER,frames[density])]
    assert len({m['pixelSha256'] for m in output_metrics[str(density)]})==4

exec((OUT/'_preview_helpers.py').read_text())

after = {str(p.relative_to(BASE)):sha(p) for phase in ORDER for p in (BASE/phase).iterdir() if p.is_file()}
assert before==after
tips = [m['centralNeedleBottomYByAlphaThreshold']['16'] for m in source_metrics]
provenance = {
    'topicId':'C01-FX-02', 'generationMode':'four built-in imagegen calls, one per phase',
    'scope':'Shared workspace artwork and deterministic staged QA only. No repository or Git writes.',
    'exporterStatus':'FX-02 is not registered/enabled here. Preview duplicates the current full-canvas render operation; this is not a canonical exporter export/check.',
    'sharedAuthoredCanvas':[1254,1254], 'placement':{'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]},
    'logicalFrame':[32,32], 'logicalAnchor':[16,16], 'logicalCanvas':[28,28],
    'phases':source_metrics,'outputs':output_metrics,
    'preservedSourceAndProvenanceHashes':before,
    'checks':{'rawAndMasterPreserved':before==after,'rawMasterByteIdenticalAll':True,'sourcePerimetersClear':True,
        'outputPerimetersClear':True,'noIndependentBboxFit':True,'noSourceCropping':True,'noOpacityNormalization':True,
        'fourDistinctSourcePixels':len({m['pixelSha256'] for m in source_metrics})==4,
        'browserObserved':False,'liveGameplayObserved':False,'registeredExportCheckRun':False},
    'registrationDiagnostics':{'centralNeedleBottomAlphaGt16Y':tips,
        'bottomTipRangeLogicalPixels':(max(tips)-min(tips))*28/1254,
        'interpretation':'Generated lower tip shifts despite shared canvas and prompts; no post-generation correction was applied. Requires art acceptance or targeted regeneration.'},
    'timing':{'fps':12,'frameStartMs':[0,1000/12,2000/12,250], 'lastFrameVisibleThroughMs':420,
        'lastFrameHoldMs':170,'nominalClipEndMs':1000/3,'apngDurationsRationalSeconds':[[1,12],[1,12],[1,12],[17,100],[3,5]],
        'apngPreviewBlankIntervalMs':600,'gameLoop':False,'previewRepetitionOnly':True},
    'limitations':['No browser playback observed','No live gameplay or white-hit-overlay/damage-number integration observed',
        'No canonical exporter changes or registration','Shared authored coordinates do not guarantee exact generated contour contact-point lock']}
(OUT/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
files = {str(p.relative_to(BASE)):sha(p) for p in OUT.iterdir() if p.is_file() and p.name!='deliverable-hashes.json'}
(OUT/'deliverable-hashes.json').write_text(json.dumps(files,indent=2)+'\n')
print(json.dumps({'source':source_metrics,'output':output_metrics,'registration':provenance['registrationDiagnostics']},indent=2))
