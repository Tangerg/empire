from PIL import Image,ImageOps,ImageDraw
from pathlib import Path
import sys,json,shutil,hashlib
root=Path('/workspace/shared/empire-art-rebuild/approved-v5/hud'); n=int(sys.argv[1]); src=Path(sys.argv[2]); d=root/f'hud-{n:02d}'
assert not (d/'master.png').exists(), 'Existing master is immutable; retain rejected version before replacement.'
for f in ('raw.png','master.png'): shutil.copyfile(src,d/f)
im=Image.open(src); alpha=im.getchannel('A'); hist=alpha.histogram(); total=im.width*im.height; bbox=alpha.point(lambda x:255 if x>=8 else 0).getbbox(); edges=list(alpha.crop((0,0,im.width,1)).getdata())+list(alpha.crop((0,im.height-1,im.width,im.height)).getdata())+list(alpha.crop((0,0,1,im.height)).getdata())+list(alpha.crop((im.width-1,0,im.width,im.height)).getdata())
hash=hashlib.sha256(src.read_bytes()).hexdigest(); ev=json.loads((d/'source-evidence.json').read_text()); ev.update({'rawGeneratedSource':str(src),'localRaw':'raw.png','sha256':hash}); (d/'source-evidence.json').write_text(json.dumps(ev,indent=2,ensure_ascii=False)+'\n')
qa={'topicId':f'C01-HUD-{n:02d}','nativeSize':list(im.size),'mode':im.mode,'alphaExtrema':alpha.getextrema(),'transparentFraction':hist[0]/total,'nearOpaqueFractionAlphaGE250':sum(hist[250:])/total,'nonzeroAlphaBBox':alpha.getbbox(),'visibleAlphaBBoxGE8':bbox,'visibleAlphaBBoxGE128':alpha.point(lambda x:255 if x>=128 else 0).getbbox(),'visibleMarginsPixelsL_T_R_B':[bbox[0],bbox[1],im.width-bbox[2],im.height-bbox[3]],'canvasEdgeMaxAlpha':max(edges),'canvasEdgeNonzeroPixelCount':sum(x>0 for x in edges),'sha256':hash,'matchesRawGeneratedBytes':(d/'raw.png').read_bytes()==(d/'master.png').read_bytes(),'identityVerified':ev['identityClarification'],'postprocessing':'None','runtimeReady':False,'reviewComplete':False,'independentPreview':{'directory':str(d/'qa-preview-32-28'),'sizes':[32,28],'method':'Full-canvas aspect-preserving fit to32, then HUD downscale to28; source unmodified.'}}
(d/'alpha-qa.json').write_text(json.dumps(qa,indent=2,ensure_ascii=False)+'\n')
p=d/'qa-preview-32-28'; p.mkdir(exist_ok=True); runtime=ImageOps.contain(im,(32,32),method=Image.Resampling.LANCZOS); runtime.save(p/'full-canvas-32.png'); hud=ImageOps.contain(runtime,(28,28),method=Image.Resampling.LANCZOS); hud.save(p/'full-canvas-28.png')
canvas=Image.new('RGB',(360,160),'#999999'); draw=ImageDraw.Draw(canvas)
for row,(name,col) in enumerate([('white','#ffffff'),('black','#000000'),('olive','#444a32')]):
 y=row*50; draw.rectangle((0,y,359,y+49),fill=col); draw.text((5,y+5),name,fill='#888888'); canvas.paste(runtime,(80,y+9),runtime); canvas.paste(hud,(135,y+11),hud)
 for sz,base in ((32,runtime),(28,hud)):
  up=base.resize((sz*2,sz*2),Image.Resampling.NEAREST)
  # enlargement is QA only, nearest neighbor and aspect preserved
 draw.text((185,y+18),'32px    28px',fill='#888888')
canvas.save(p/'actual-size-backgrounds.png'); print(json.dumps(qa))
