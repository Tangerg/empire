from PIL import Image,ImageDraw
from pathlib import Path
import json,hashlib,sys
root=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');sys.path.insert(0,str(root/'tools/art'));from export_overrides import fit_canvas,validate_alpha
p=root/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/hud';out=root/'docs/art-rebuild-20261019/approved-v5/scifi-hud-next-four'
names=['breakable','engineering','escort','evacuation'];board=Image.new('RGB',(1200,980),'#cccccc');draw=ImageDraw.Draw(board);result=[]
for i,n in enumerate(names):
 d=p/f'c02-hud-{n}';im=Image.open(d/'master.png');qa={'source':{'mode':im.mode,'size':im.size,**validate_alpha(im,n)}};rt,fit=fit_canvas(im,(32,32),2);rt.save(d/'native32.png');qa['runtime']={'fit':fit,**validate_alpha(rt,'native32')};qa['hashes']={x:hashlib.sha256((d/x).read_bytes()).hexdigest() for x in ['raw.png','master.png','prompt.txt','native32.png']};(d/'alpha-qa.json').write_text(json.dumps(qa,indent=2));result.append({'topicId':f'c02-hud-{n}','source':str((d/'master.png').relative_to(root)),'sha256':qa['hashes']['master.png'],'alpha':qa})
 draw.text((i*300+10,8),n,fill='black')
 for j,(bg,label) in enumerate([('white','white'),('#15191d','black'),('#74794a','olive')]):
  tile=Image.new('RGBA',(300,300),bg);small=im.copy();small.thumbnail((285,285));tile.alpha_composite(small,((300-small.width)//2,(300-small.height)//2));tile.convert('RGB').save(d/f'review256-{label}.png');board.paste(tile.convert('RGB'),(i*300,30+j*310));board.paste(bg,(i*300,930+j*16,i*300+300,946+j*16))
  view=Image.new('RGBA',(300,60),bg);view.alpha_composite(rt,(15,14));view.alpha_composite(rt.resize((128,128),Image.Resampling.NEAREST).crop((0,0,128,60)),(70,0)) if False else None
  # Separate native-size and honest nearest-neighbor magnification.
  inspect=Image.new('RGBA',(220,170),bg);inspect.alpha_composite(rt,(8,8));inspect.alpha_composite(rt.resize((128,128),Image.Resampling.NEAREST),(70,20));inspect.convert('RGB').save(d/f'native32-review-{label}.png')
board.save(out/'source-contact-sheet.png');(out/'integration-candidates.json').write_text(json.dumps(result,indent=2))
smallboard=Image.new('RGB',(880,540),'#cccccc')
for i,n in enumerate(names):
 for j,label in enumerate(['white','black','olive']):smallboard.paste(Image.open(p/f'c02-hud-{n}'/f'native32-review-{label}.png'),(i*220,30+j*170))
draw=ImageDraw.Draw(smallboard)
for i,n in enumerate(names):draw.text((i*220+8,8),n,fill='black')
smallboard.save(out/'native32-review.png');print(json.dumps([{k:v for k,v in a.items() if k!='alpha'} for a in result],indent=2))
