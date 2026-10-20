from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib,sys
R=Path(__file__).resolve().parents[4]
B=R/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/antler-knight'
D=Path(__file__).parent
sys.path.insert(0,str(R/'tools/art'))
from assemble_approved_slice import alpha_report
names=['idle','walk-a','attack','walk-b']; rows={}; imgs={}
offsets={'idle':[0,-1],'walk-a':[-2,0],'attack':[2,2],'walk-b':[1,0]}
for n in names:
 p=B/n/'master.png';im=Image.open(p);a=alpha_report(im)
 rows[n]={'source':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'rawByteIdentical':p.read_bytes()==(B/n/'raw.png').read_bytes(),'size':im.size,'alpha':a}
 (B/n/'alpha-qa.json').write_text(json.dumps(rows[n],indent=2)+'\n')
 imgs[n]=im
for size in [32,64]:
 sheet=Image.new('RGBA',(size*4,size));review=Image.new('RGB',(size*4,size*2),'white')
 for k,n in enumerate(names):
  # All phases share one full-canvas transform. Integer 64px translation recommendations only.
  canvas=Image.new('RGBA',(64,64));canvas.paste(imgs[n].resize((52,52),Image.Resampling.LANCZOS),(3+offsets[n][0],10+offsets[n][1]))
  if size==32:canvas=canvas.resize((32,32),Image.Resampling.LANCZOS)
  canvas.save(D/f'{n}-native{size}.png');sheet.paste(canvas,(k*size,0))
  review.paste(canvas,(k*size,0),canvas);black=Image.new('RGB',(size,size),'#151515');black.paste(canvas,(0,0),canvas);review.paste(black,(k*size,size))
  try: rows[n][f'native{size}Alpha']=alpha_report(canvas)
  except ValueError as exc:
   if size==64: raise
   rows[n][f'native{size}Alpha']={'secondaryZoomOnly':True,'error':str(exc)}
 sheet.save(D/f'quartet-native{size}.png');review.save(D/f'quartet-native{size}-white-black.png');review.resize((size*4*4,size*2*4),Image.Resampling.NEAREST).save(D/f'quartet-native{size}-white-black-4x.png')
(D/'alpha-qa.json').write_text(json.dumps(rows,indent=2)+'\n')
measured=json.loads((D/'measured-support.json').read_text())
for n in names:
 x,y=measured[n]['support'];dx,dy=offsets[n]
 projected=[3+dx+x*52/1254,10+dy+y*52/1254]
 measured[n]['projectedSupport']=projected
 measured[n]['canonicalAnchorError']=[projected[0]-32,projected[1]-63]
 measured[n]['canvasBounds']=[3+dx,10+dy,3+dx+52,10+dy+52]
 assert all(abs(v)<=2 for v in offsets[n])
 assert all(0<=v<=64 for v in measured[n]['canvasBounds'])
reg={'status':'Bounded diagnostic candidate only; anchor mismatch prevents readiness claim','frameSize':[64,64],'canonicalAnchor':[32,63],'commonSourceCanvas':[1254,1254],'renderWidth':52,'sharedScale':52/1254,'origin':[3,10],'registrationOffsets':offsets,'measuredSupport':measured,'method':'Planted contact X is equal-weight mean of each planted hoof bottom-four-row mean X. Y is lowest planted sole. Visually lifted hooves are excluded. ROIs and exact sole measurements recorded. No torso/mass-derived offsets.','sourcePixelModification':False,'formalRuntimeIntegration':False,'fullCanvasFeasible':True,'maximumAttackSupportYWithFullCanvas':64-52+1152*52/1254,'anchorLimitation':'At width52, attack full canvas must end no lower than64; its lowest planted sole is source y1152 of1254, hence support cannot exceed59.7703. Cannot reach canonical63 without cropping full canvas, changing scale drastically, or regenerating. None done.','residualDriftXY':[max(v['projectedSupport'][i] for v in measured.values())-min(v['projectedSupport'][i] for v in measured.values()) for i in (0,1)]}
(B/'registration-evidence.json').write_text(json.dumps(reg,indent=2)+'\n')
manifest={'asset':'C02-SFV1-unit-antler-knight','topicId':'c02-unit-antler-knight','status':'Complete native authored quartet; integration pending','tool':'built-in image_gen','frameOrder':names,'commonSourceCanvas':[1254,1254],'masterPolicy':'Byte-identical generated native RGBA output; no alpha cleanup, painting, crop, resize, or recolor of masters. QA projections are separate.','poses':rows,'registrationEvidence':str((B/'registration-evidence.json').relative_to(R)),'formalRuntimeIntegration':False}
(B/'bundle-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps(rows,indent=2))
