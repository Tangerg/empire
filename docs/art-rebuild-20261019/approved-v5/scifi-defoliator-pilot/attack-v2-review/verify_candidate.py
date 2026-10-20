from pathlib import Path
from PIL import Image
import sys,json,hashlib
R=Path(__file__).resolve().parents[5];D=Path(__file__).parent;P=D.parent;B=R/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/defoliator-pilot'
sys.path.insert(0,str(R/'tools/art'))
from export_frame_overrides import plan
from assemble_approved_slice import alpha_report
p=B/'attack/raw-v2.png';qa={'sourceAlpha':alpha_report(Image.open(p)),'sourceHash':hashlib.sha256(p.read_bytes()).hexdigest(),'frozenUnchanged':{}}
for name,sha in json.loads((P/'frozen-quartet-sha256.json').read_text()).items():
 actual=hashlib.sha256((B/name/'master.png').read_bytes()).hexdigest();assert actual==sha;qa['frozenUnchanged'][name]=True
c=json.loads((P/'temporary-frame-overrides.json').read_text());a=c['assets']['c02-unit-defoliator-pilot']['frames']['attack'];a['source']=str(p.relative_to(R));a['sha256']=qa['sourceHash'];a['registrationOffset']=[1,1]
f=D/'temporary-frame-overrides.json';f.write_text(json.dumps(c,indent=2)+'\n');_,sheet,report=plan(R,f)[0];sheet.save(D/'canonical-plan-1x.png');(D/'temporary-plan-verification.json').write_text(json.dumps(report,indent=2)+'\n');(D/'candidate-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
old=Image.open(P/'canonical-plan-1x.png');review=Image.new('RGB',(256,64*6))
for i,bg in enumerate(['white','#171717','#777d56']):
 for j,s in enumerate([old,sheet]):
  row=Image.new('RGB',(256,64),bg);row.paste(s,(0,0),s);review.paste(row,(0,(i*2+j)*64))
review.save(D/'frozen-vs-candidate-native64.png');review.resize((1024,1536),Image.Resampling.NEAREST).save(D/'frozen-vs-candidate-native64-4x.png');print('PASS unchanged source/native guards, isolated candidate plan, frozen master hashes unchanged.')
