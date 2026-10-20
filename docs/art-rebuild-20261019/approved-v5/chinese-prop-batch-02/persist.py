import json,hashlib,shutil,sys
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[4];sys.path.insert(0,str(ROOT/'tools/art'))
from export_overrides import fit_canvas,validate_alpha
DOC=Path(__file__).resolve().parent
PACK=ROOT/'packages/story-candidate-03/assets/final-ancient-china-v1'
ATLAS=PACK/'masters/props/props-board-01.png'
STYLE=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/props/bprop-engineering-1/master.png'
DATA=[('27872fa1','8a3e4e935c3d430cb70c6e66a5277923','fourth row fifth object; semantic mapping, original crop mapping unverified'),('2e0af0f2','3bd3ec38aa114a5981f92ca90d9a838f','second row fifth object'),('2e7df413','a23a6686317f479f8805e06c59bb0640','bottom row third object'),('2f038eea','5d23ef182b5141bab93c6563998fd4e1','third row fifth object')]
PROMPTS=json.loads((DOC/'prompts.json').read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assets={a['topicId']:a for a in json.loads((PACK/'manifest-final-ancient-china-v1.json').read_text())['assets']}
rows=[];catalog={};sheet=Image.new('RGB',(400,420),'#dddddd');draw=ImageDraw.Draw(sheet)
for i,((suffix,call,identity),prompt) in enumerate(zip(DATA,PROMPTS)):
 topic='c03-battle-prop-'+suffix;a=assets[topic]; dest=PACK/'masters/approved-v5/props'/topic;dest.mkdir(parents=True,exist_ok=True)
 raw=ROOT.parent/'generated_images'/('call_'+call+'.png')
 for filename in ['raw.png','master.png']:shutil.copyfile(raw,dest/filename)
 (dest/'prompt.txt').write_text(prompt+'\n')
 source=Image.open(dest/'master.png');qa=validate_alpha(source,topic);native,contract=fit_canvas(source,(64,64),2);native.save(DOC/(topic+'-64.png'))
 spec={'source':str((dest/'master.png').relative_to(ROOT)),'sha256':sha(dest/'master.png')};catalog[topic]=spec
 evidence={'topicId':topic,'assetId':a['assetId'],'label':a['label'],'tool':'built-in image_gen.imagegen','transparent_background':True,'rawGeneratedSource':str(raw),'sha256':spec['sha256'],'originalFullAtlas':{'path':str(ATLAS.relative_to(ROOT)),'sha256':sha(ATLAS),'identityLocation':identity},'styleReference':{'path':str(STYLE.relative_to(ROOT)),'sha256':sha(STYLE)},'generationCallId':call,'promptFile':'prompt.txt','promptSha256':hashlib.sha256(prompt.encode()).hexdigest(),'promptFileSha256':sha(dest/'prompt.txt'),'postprocessing':'None; raw.png and master.png are byte-identical copies of native output. Review export alone uses existing whole-canvas contain contract. No crop, recolor or alpha stripping.','nativeSize':list(source.size),'sourceAlpha':qa,'exportContract':contract,'outputAlpha':validate_alpha(native,'native'),'registered':False}
 (dest/'source-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n');(dest/'alpha-qa.json').write_text(json.dumps({'source':qa,'output':evidence['outputAlpha']},indent=2)+'\n')
 rows.append({**a,**spec,'reviewPng':str((DOC/(topic+'-64.png')).relative_to(ROOT)),'evidence':str((dest/'source-evidence.json').relative_to(ROOT))})
 draw.text((8,6+i*104),topic,fill='black')
 for j,color in enumerate(['white','black','#727b43']):
  tile=Image.new('RGBA',(64,64),color);tile.alpha_composite(native);sheet.paste(tile.convert('RGB'),(15+j*130,27+i*104))
 sheet.save(DOC/'native-review-white-black-olive.png')
(DOC/'integration-candidates.json').write_text(json.dumps({'schemaVersion':1,'assets':catalog},indent=2)+'\n')
(DOC/'delivery-manifest.json').write_text(json.dumps({'status':'candidate masters, not formally registered','assets':rows},ensure_ascii=False,indent=2)+'\n')
print(json.dumps(rows,ensure_ascii=False,indent=2))
