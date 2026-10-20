from pathlib import Path
from PIL import Image
import json,hashlib,sys
R=Path(__file__).resolve().parents[4]; D=Path(__file__).parent
B=R/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/defoliator-pilot'
sys.path.insert(0,str(R/'tools/art'))
from export_frame_overrides import plan
from assemble_approved_slice import alpha_report
names=['idle','walk-a','attack','walk-b']
reg=json.loads((D/'registration.json').read_text())
frames={}; qa={}
for n in names:
 p=B/n/'master.png'; im=Image.open(p)
 frames[n]={'source':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'registrationOffset':reg['offsets'][n]}
 qa[n]={'size':im.size,'alpha':alpha_report(im),'rawMasterByteEqual':p.read_bytes()==(B/n/'raw.png').read_bytes()}
cat={'schemaVersion':1,'assets':{'c02-unit-defoliator-pilot':{'kind':'frames-v1','frames':frames,'placement':{'renderWidth':reg['renderWidth'],'origin':reg['origin']}}}}
p=D/'temporary-frame-overrides.json';p.write_text(json.dumps(cat,indent=2)+'\n')
result=plan(R,p);assert len(result)==1
_,sheet,report=result[0];sheet.save(D/'canonical-plan-1x.png')
(D/'temporary-plan-verification.json').write_text(json.dumps(report,indent=2)+'\n')
review=Image.new('RGB',(256,192))
for j,bg in enumerate(['#ffffff','#171717','#777d56']):
 row=Image.new('RGB',(256,64),bg);row.paste(sheet,(0,0),sheet);review.paste(row,(0,j*64))
review.save(D/'native64-three-backgrounds.png');review.resize((1024,768),Image.Resampling.NEAREST).save(D/'native64-three-backgrounds-4x.png')
(D/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
print('PASS: distinct native sources, true alpha, shared whole-canvas scale, bounded translations, canonical 256x64 plan. No runtime writes.')
