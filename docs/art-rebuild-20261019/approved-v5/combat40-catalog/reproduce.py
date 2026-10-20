from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib
p=Path('packages/story-candidate-01/assets/final-fantasy-v1');m=json.load(open(p/'manifest-final-fantasy-v1.json'));h=json.load(open(p/'manifest-tactical-runtime-hd.json'));hd={a['topicId']:a for a in h['assets']};assets=[a for a in m['assets'] if a['category']=='combat-unit'];assert len(assets)==40
d=Path('docs/art-rebuild-20261019/approved-v5/combat40-catalog');d.mkdir(exist_ok=True);out=Image.new('RGB',(160*8*2,112*5*2),'#69794e');dr=ImageDraw.Draw(out);rows=[]
for i,a in enumerate(assets):
 x=(i%8)*320;y=(i//8)*224;f=p/hd[a['topicId']]['png'];im=Image.open(f).convert('RGBA').crop((0,0,a['frameWidth']*2,a['frameHeight']*2));ax,ay=a['anchor'];pos=(round(x+160-ax*2),round(y+176-ay*2));out.paste(im,pos,im);dr.line((x+20,y+176,x+300,y+176),fill='#849071');dr.text((x+8,y+196),a['topicId'].removeprefix('C01-UNIT-'),fill='white');rows.append({'topicId':a['topicId'],'source':str(f),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'frame':0,'density':2,'anchor':a['anchor'],'paste':pos})
out.save(d/'all40-idle-native2x.png');(d/'provenance.json').write_text(json.dumps({'meaning':'All40 canonical combat idle frames at actual2x density, shared displayed footing. Catalog comparison, not deployed formation or live screenshot; all four poses tested separately. Named actor variants and red faction variant are not added to40 count.','assets':rows},indent=2)+'\n')
