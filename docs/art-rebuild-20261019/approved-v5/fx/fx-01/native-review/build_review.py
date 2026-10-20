#!/usr/bin/env python3
"""Non-destructive FX-01 QA exports; no artwork generation or repository writes."""
import hashlib, json, shutil, struct, subprocess, sys, zlib
from pathlib import Path
sys.dont_write_bytecode = True
from PIL import Image, ImageDraw, ImageFont
import numpy as np

REPO=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
BASE=Path('/workspace/shared/empire-art-rebuild/approved-v5/fx/fx-01')
OUT=Path(__file__).resolve().parent
FIXTURE=OUT/'export-fixture'
ORDER=['onset','sweep','peak','dissipate']
BGS={'light':'#f1e8d3','dark':'#292725','olive':'#626b43','white':'#ffffff','black':'#000000'}
PACK=Path('packages/story-candidate-01/assets/final-fantasy-v1')
sys.path.insert(0,str(REPO/'tools/art'))
from export_fx_overrides import export, validate_fx_alpha
from export_overrides import PACKS

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def pix(im): return hashlib.sha256(im.tobytes()).hexdigest()
def metric(im):
    a=np.array(im.getchannel('A'))
    ys,xs=np.indices(a.shape)
    mass=int(a.sum())
    return {**validate_fx_alpha(im,'QA'), 'sourceMode':im.mode, 'size':list(im.size), 'pixelSha256':pix(im),
            'alphaLevels':len(np.unique(a)), 'transparentPixels':int((a==0).sum()),
            'partialAlphaPixels':int(((a>0)&(a<255)).sum()),
            'nearOpaquePixels':int((a>=240).sum()),'alphaEquivalentPixels':round(mass/255,6),
            'alphaGt1BoundingBox':list(im.getchannel('A').point(lambda v:255 if v>1 else 0).getbbox()),
            'alphaGt16BoundingBox':list(im.getchannel('A').point(lambda v:255 if v>16 else 0).getbbox()),
            'alphaWeightedCentroid':[round(float((a*xs).sum()/mass),5),round(float((a*ys).sum()/mass),5)]}

before={str(p.relative_to(BASE)):sha(p) for phase in ORDER for p in (BASE/phase).iterdir() if p.is_file()}
for campaign,directory,suffix in PACKS:
    rel=Path('packages')/campaign/'assets'/directory
    (FIXTURE/rel).mkdir(parents=True,exist_ok=True)
    for filename in [f'manifest-final-{suffix}-v1.json','manifest-tactical-runtime-hd.json']:
        p=REPO/rel/filename
        if p.exists(): shutil.copyfile(p,FIXTURE/rel/filename)
phases=[]
source_metrics=[]
for phase in ORDER:
    raw=BASE/phase/'raw.png'; master=BASE/phase/'master.png'
    dest=PACK/'masters/fx-review/C01-FX-01'/f'{phase}.png'
    (FIXTURE/dest).parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(raw,FIXTURE/dest)
    phases.append({'phase':phase,'source':str(dest),'sha256':sha(raw)})
    im=Image.open(raw)
    source_metrics.append({'phase':phase,'raw':str(raw),'master':str(master),'sha256':sha(raw),
        'rawMasterByteIdentical':raw.read_bytes()==master.read_bytes(),**metric(im)})
placement={'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]}
catalog={'schemaVersion':1,'assets':{'C01-FX-01':{'kind':'fx-phases-v1','phases':phases,'placement':placement}}}
(OUT/'review-catalog.json').write_text(json.dumps(catalog,indent=2)+'\n')
reports=export(FIXTURE,OUT/'review-catalog.json')
check=export(FIXTURE,OUT/'review-catalog.json',True)
assert reports==check
(OUT/'export-report.json').write_text(json.dumps(reports,indent=2)+'\n')
(OUT/'export-check.json').write_text(json.dumps(check,indent=2)+'\n')
frames={}
for report in reports:
    d=report['density']; sheet=Image.open(FIXTURE/report['output']); side=32*d
    sheet.save(OUT/f'fx-01@{d}x.png')
    frames[d]=[]
    for i,phase in enumerate(ORDER):
        im=sheet.crop((i*side,0,(i+1)*side,side)); im.save(OUT/f'{phase}@{d}x.png'); frames[d].append(im)

font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
small=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',12)
def composite(im,bg):
    x=Image.new('RGBA',im.size,bg); x.alpha_composite(im); return x.convert('RGB')
for d in [1,2]:
    side=32*d; cell=max(112,side+36); width=128+cell*4; row=side+24
    board=Image.new('RGB',(width,80+row*len(BGS)), '#e7e7e7'); draw=ImageDraw.Draw(board)
    draw.text((12,9),f'C01-FX-01: native {side}px / {d}x density - unchanged full-canvas export',fill='#111111',font=font)
    draw.text((12,30),'28px shared logical canvas at [2,2]; normal source-over; no scene overlays',fill='#333333',font=small)
    for i,phase in enumerate(ORDER):draw.text((132+i*cell,56),phase,fill='#111111',font=font)
    for r,(name,bg) in enumerate(BGS.items()):
        y=80+r*row; draw.text((12,y+side//2-7),name,fill='#111111',font=font)
        for i,im in enumerate(frames[d]):
            x=128+i*cell; draw.rectangle((x,y,x+cell-8,y+side+12),fill=bg)
            board.paste(composite(im,bg),(x+(cell-8-side)//2,y+6))
    board.save(OUT/f'native-{side}px-five-backgrounds.png')

# Magnification is explicitly diagnostic only, never presented as native art size.
board=Image.new('RGB',(1120,900),'#d9d9d9'); draw=ImageDraw.Draw(board)
draw.text((12,10),'Diagnostic zoom: 64px exports enlarged 4x nearest-neighbor',fill='#111111',font=font)
for r,(name,bg) in enumerate(list(BGS.items())[:3]):
    y=64+r*276
    for i,phase in enumerate(ORDER):
        x=16+i*276; draw.text((x,y-22),f'{name} / {phase}',fill='#111111',font=font)
        board.paste(composite(frames[2][i],bg).resize((256,256),Image.Resampling.NEAREST),(x,y))
board.save(OUT/'diagnostic-64px-nearest-4x.png')

# APNG encodes exact rational 12 fps intervals, not GIF's centisecond approximation.
def chunk(kind,data): return struct.pack('!I',len(data))+kind+data+struct.pack('!I',zlib.crc32(kind+data)&0xffffffff)
def apng(path,seq,durations):
    w,h=seq[0].size; stream=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',w,h,8,6,0,0,0))+chunk(b'acTL',struct.pack('!2I',len(seq),0)); sn=0
    for i,(im,(num,den)) in enumerate(zip(seq,durations)):
        stream+=chunk(b'fcTL',struct.pack('!5I2H2B',sn,w,h,0,0,num,den,0,0)); sn+=1
        rgba=im.convert('RGBA'); data=rgba.tobytes(); encoded=zlib.compress(b''.join(b'\x00'+data[y*w*4:(y+1)*w*4] for y in range(h)))
        if i==0:stream+=chunk(b'IDAT',encoded)
        else:stream+=chunk(b'fdAT',struct.pack('!I',sn)+encoded);sn+=1
    stream+=chunk(b'IEND',b'');path.write_bytes(stream)
for d in [1,2]:
    side=32*d
    for name,bg in BGS.items():
        seq=[composite(im,bg).convert('RGBA') for im in frames[d]]+[Image.new('RGBA',(side,side),bg)]
        apng(OUT/f'playback-{name}@{d}x.apng',seq,[(1,12),(1,12),(1,12),(17,100),(3,5)])

html='''<!doctype html><html><meta charset="utf-8"><title>FX-01 staged phase review</title><style>
body{font:16px system-ui,sans-serif;background:#e7e7e7;color:#171717;margin:24px}h1{font-size:22px}p{max-width:1050px}button,input{font:inherit;margin:6px}table{border-collapse:collapse}td,th{padding:12px 22px;text-align:left}canvas{image-rendering:pixelated;display:block}.tile{display:flex;align-items:center;justify-content:center;min-width:110px;height:88px}.lab{font-size:12px}#state{font-variant-numeric:tabular-nums}
</style><h1>C01-FX-01 staged slash review</h1><p>Exact shared transform: 1254×1254 → 28×28 logical pixels at [2,2] inside each 32px frame; source pivot [627,627] → [16,16]. Native 32px and 64px previews use normal source-over. This is a staged art preview, not live gameplay; the game's independent white hit burst and damage number are not included.</p>
<p>12 fps: onset 0–83⅓ms, sweep 83⅓–166⅔ms, peak 166⅔–250ms, dissipate 250–420ms, then removed. The preview repeats after a 600ms clear interval; the game effect itself does not loop.</p>
<div><button id="play">Play 420ms hit</button><button id="repeat">Repeat preview</button><button id="pause">Pause</button><input id="scrub" type="range" min="0" max="1020" step="0.01" value="0"><span id="state"></span></div>
<table><thead><tr><th>Background</th><th>32px native</th><th>64px native / HD</th><th>HD at 32 CSS px</th><th>64px at 4× diagnostic zoom</th></tr></thead><tbody id="rows"></tbody></table>
<script>
const phases=['onset','sweep','peak','dissipate'];const bgs=BGJSON;const images={};const panels=[];let running=false,repeating=false,start=0,currentTime=0,history=[];
const rowRoot=document.querySelector('#rows');for(const [name,bg] of Object.entries(bgs)){const row=document.createElement('tr');const label=document.createElement('th');label.textContent=name;row.append(label);for(const [density,displaySize] of [[1,32],[2,64],[2,32],[2,256]]){const td=document.createElement('td'),c=document.createElement('canvas');c.width=c.height=32*density;c.style.width=c.style.height=displaySize+'px';c.dataset.background=name;c.dataset.density=density;c.dataset.displaySize=displaySize;td.style.background=bg;td.append(c);row.append(td);panels.push({canvas:c,bg,density});}rowRoot.append(row);}
function phaseAt(ms){return ms>=420?-1:Math.min(3,Math.floor(Math.max(0,ms)*12/1000));}
function renderAt(ms){currentTime=ms;const phase=phaseAt(ms);for(const p of panels){const ctx=p.canvas.getContext('2d');ctx.clearRect(0,0,p.canvas.width,p.canvas.height);ctx.fillStyle=p.bg;ctx.fillRect(0,0,p.canvas.width,p.canvas.height);if(phase>=0)ctx.drawImage(images[p.density][phase],0,0);}document.querySelector('#scrub').value=ms;document.querySelector('#state').textContent=ms.toFixed(2)+'ms · '+(phase<0?'removed':phases[phase]);document.body.dataset.phase=String(phase);document.body.dataset.time=String(ms);return phase;}
function tick(now){if(!running)return;const t=now-start;const ms=repeating?t%1020:Math.min(t,420);const phase=renderAt(ms);if(!history.length||history.at(-1).phase!==phase)history.push({time:ms,phase,clock:now});if(!repeating&&t>=420){running=false;return;}requestAnimationFrame(tick);}
function play(repeat){running=true;repeating=repeat;history=[];start=performance.now();renderAt(0);requestAnimationFrame(tick);}
document.querySelector('#play').onclick=()=>play(false);document.querySelector('#repeat').onclick=()=>play(true);document.querySelector('#pause').onclick=()=>{running=false};document.querySelector('#scrub').oninput=e=>{running=false;renderAt(Number(e.target.value))};
window.fxReview={phaseAt,renderAt,play,get history(){return history},get currentTime(){return currentTime},get ready(){return Object.keys(images).length===2}};
Promise.all([1,2].map(async density=>{images[density]=await Promise.all(phases.map(phase=>new Promise(resolve=>{const im=new Image();im.onload=()=>resolve(im);im.src=phase+'@'+density+'x.png';})));})).then(()=>{renderAt(0);document.body.dataset.ready='true'});
</script></html>'''.replace('BGJSON',json.dumps(BGS))
(OUT/'playback.html').write_text(html)
output_metrics={str(d):[{'phase':p,**metric(im)} for p,im in zip(ORDER,frames[d])] for d in [1,2]}
after={str(p.relative_to(BASE)):sha(p) for phase in ORDER for p in (BASE/phase).iterdir() if p.is_file()}
assert before==after, 'Raw source/provenance changed during review'
codefiles=['tools/art/export_fx_overrides.py','tools/art/export_overrides.py','tools/art/fx-export-contract.md']
code=[]
for rel in codefiles:
    old=subprocess.check_output(['git','show','49b4afc:'+rel],cwd=REPO)
    assert old==(REPO/rel).read_bytes(),f'{rel} differs from requested exporter commit'
    code.append({'path':rel,'sha256':sha(REPO/rel),'matchesCommit49b4afc':True})
provenance={'topicId':'C01-FX-01','scope':'Staged art QA only; no new generation, repository changes, registration or live gameplay',
 'sourceCommitRequested':'49b4afc6c5b4cde520ee8258a74c8140a5780657','inspectedRepoHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=REPO,text=True).strip(),
 'exporterCode':code,'placement':placement,'sharedAuthoredCanvas':[1254,1254],'logicalCanvas':[28,28],
 'logicalFrame':[32,32],'logicalAnchor':[16,16],'phases':source_metrics,'outputs':output_metrics,
 'checks':{'rawAndMasterPreserved':before==after,'rawMasterIdenticalAll':all(x['rawMasterByteIdentical'] for x in source_metrics),'exactExporterExportThenCheck':reports==check,
 'sharedCanvas':True,'sourcePerimetersClear':True,'outputPerimetersClear':True,'fourDistinctSourcePixels':len(set(x['pixelSha256'] for x in source_metrics))==4,'noIndependentBboxFit':True,'noOpacityNormalization':True},
 'rawAndProvenanceHashesBeforeAndAfter':before,
 'timing':{'fps':12,'frameStartMs':[0,1000/12,2000/12,250],'lastFrameVisibleThroughMs':420,'lastFrameHoldMs':170,'nominalClipEndMs':1000/3,
 'apngDurationsRationalSeconds':[[1,12],[1,12],[1,12],[17,100],[3,5]],'apngPreviewBlankIntervalMs':600,'gameLoop':False,'previewRepetitionOnly':True},
 'reviewRequiredSeparately':['Visual crescent progression','Backdrop/fringe review','Staged 12fps playback','Live gameplay with burst/number overlay (not performed)']}
(OUT/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
print(json.dumps({'output':str(OUT),'source':source_metrics,'outputMetrics':output_metrics},indent=2))
