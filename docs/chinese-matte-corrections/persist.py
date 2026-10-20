import hashlib,json,shutil,sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageOps
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/art'))
from export_overrides import fit_canvas,validate_alpha
DOC=Path(__file__).resolve().parent
STYLE=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/props/bprop-engineering-1/master.png'
DATA=[('27872fa1','exec-c816b53a-35a5-41cb-a43e-90b2a0b4ebe6'),('2f038eea','exec-fe6284b1-8def-45de-a890-2bb338d76f4f')]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rows=[]
comparison=Image.new('RGB',(1200,830),'#dddddd');draw=ImageDraw.Draw(comparison)
native=Image.new('RGB',(720,240),'#dddddd');nd=ImageDraw.Draw(native)
for i,(suffix,call) in enumerate(DATA):
 topic='c03-battle-prop-'+suffix
 old=ROOT/'packages/story-candidate-03/assets/final-ancient-china-v1/masters/approved-v5/props'/topic
 dest=old/'candidates/broad-matte-v2'
 raw=ROOT.parent/'generated_images'/(call+'.png')
 for name in ['raw.png','master.png']:shutil.copyfile(raw,dest/name)
 src=Image.open(dest/'master.png');qa=validate_alpha(src,topic);out,contract=fit_canvas(src,(64,64),2);out.save(dest/'native64.png')
 originals=[STYLE,old/'master.png',dest/'master.png']
 for j,p in enumerate(originals):
  img=Image.open(p);display=ImageOps.contain(img,(385,385),Image.Resampling.LANCZOS)
  comparison.paste(display,(j*400+(400-display.width)//2,i*415+25),display)
  draw.text((j*400+8,i*415+6),['Approved reference','Original '+suffix,'Matte v2 '+suffix][j],fill='black')
  small,_=fit_canvas(img,(64,64),2)
  for k,color in enumerate(['white','black','#727b43']):
   tile=Image.new('RGBA',(64,64),color);tile.alpha_composite(small);native.paste(tile.convert('RGB'),(j*240+k*72+6,i*120+30))
  nd.text((j*240+6,i*120+8),['Reference','Original '+suffix,'Matte v2 '+suffix][j],fill='black')
 evidence={'topicId':topic,'tool':'built-in image_gen.imagegen','generationOutputId':call,'transparent_background':True,'rawGeneratedSource':str(raw),'sha256':sha(raw),'promptFile':'prompt.txt','promptFileSha256':sha(dest/'prompt.txt'),'references':[{'role':role,'path':str(p.relative_to(ROOT)),'sha256':sha(p)} for role,p in [('edit-target',old/'master.png'),('approved-style-only',STYLE)]],'postprocessing':'None. raw.png and master.png are byte-identical native imagegen output. native64.png is review-only whole-canvas-contain LANCZOS export, padding 2. No smoothing, recolor, crop, alpha replacement or source pixel operations.','sourceSize':list(src.size),'sourceAlpha':qa,'exportContract':contract,'outputAlpha':validate_alpha(out,topic+' native64'),'selected':False,'newTopicCount':0}
 (dest/'source-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
 (dest/'alpha-qa.json').write_text(json.dumps({'source':qa,'output':evidence['outputAlpha']},indent=2)+'\n')
 rows.append({'topicId':topic,'source':str((dest/'master.png').relative_to(ROOT)),'sha256':sha(dest/'master.png'),'evidence':str((dest/'source-evidence.json').relative_to(ROOT)),'sourceAlpha':qa,'outputAlpha':evidence['outputAlpha']})
comparison.save(DOC/'full-source-comparison.png');native.save(DOC/'native64-comparison.png')
(DOC/'candidate-evidence.json').write_text(json.dumps({'status':'unselected source correction candidates','newTopicCount':0,'assets':rows},indent=2)+'\n')
print(json.dumps(rows,indent=2))
