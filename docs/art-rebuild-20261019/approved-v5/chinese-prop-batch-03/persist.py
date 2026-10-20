import json,hashlib,shutil,sys,re
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[4];sys.path.insert(0,str(ROOT/'tools/art'))
from export_overrides import fit_canvas,validate_alpha
DOC=Path(__file__).resolve().parent
PACK=ROOT/'packages/story-candidate-03/assets/final-ancient-china-v1'
ATLAS=PACK/'masters/props/props-board-01.png'
STYLE=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/props/bprop-engineering-1/master.png'
records=json.loads((DOC/'generation-records.json').read_text()); assets={a['topicId']:a for a in json.loads((PACK/'manifest-final-ancient-china-v1.json').read_text())['assets']}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def edge(im):
 a=im.getchannel('A');w,h=im.size;v=[a.getpixel((x,y)) for x in range(w) for y in [0,h-1]]+[a.getpixel((x,y)) for y in range(1,h-1) for x in [0,w-1]]
 return {'nonzero':sum(x>0 for x in v),'maximum':max(v)}
rows=[];catalog={};sheet=Image.new('RGB',(400,420),'#dddddd');draw=ImageDraw.Draw(sheet)
for i,(s,r) in enumerate(zip(records['specs'],records['results'])):
 topic='c03-battle-prop-'+s['suffix'];a=assets[topic];dest=PACK/'masters/approved-v5/props'/topic;dest.mkdir(parents=True,exist_ok=True)
 raw=Path(re.findall(r'/workspace/[^\s]+\.png',r['output_hint'])[0])
 for name in ['raw.png','master.png']:shutil.copyfile(raw,dest/name)
 (dest/'prompt.txt').write_text(s['prompt']+'\n')
 source=Image.open(dest/'master.png');qa=validate_alpha(source,topic);native,contract=fit_canvas(source,(64,64),2);native.save(DOC/(topic+'-64.png'))
 spec={'source':str((dest/'master.png').relative_to(ROOT)),'sha256':sha(dest/'master.png')};catalog[topic]=spec
 evidence={'topicId':topic,'assetId':a['assetId'],'label':a['label'],'tool':'built-in image_gen.imagegen','transparent_background':True,'rawGeneratedSource':str(raw),'generationOutputHint':r['output_hint'],'sha256':spec['sha256'],'rawMasterByteIdentical':sha(dest/'raw.png')==sha(dest/'master.png'),'originalFullAtlas':{'path':str(ATLAS.relative_to(ROOT)),'sha256':sha(ATLAS),'identityLocation':s['identity'],'identityMethod':'Semantic reconstruction from complete atlas and canonical label, not a claim of verified historical crop coordinates.'},'styleReference':{'path':str(STYLE.relative_to(ROOT)),'sha256':sha(STYLE)},'promptFile':'prompt.txt','promptSha256':sha(dest/'prompt.txt'),'postprocessing':'None. Exact native raw/master bytes preserved. Review export only: existing full-canvas contain fit, 64x64 padding2. No crop or pixel cleanup.','nativeSize':list(source.size),'sourceAlpha':qa,'sourceEdges':edge(source),'exportContract':contract,'outputAlpha':validate_alpha(native,'native'),'outputEdges':edge(native),'registered':False}
 (dest/'source-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n');(dest/'alpha-qa.json').write_text(json.dumps({k:evidence[k] for k in ['sourceAlpha','sourceEdges','outputAlpha','outputEdges']},indent=2)+'\n')
 rows.append({**a,**spec,'reviewPng':str((DOC/(topic+'-64.png')).relative_to(ROOT)),'evidence':str((dest/'source-evidence.json').relative_to(ROOT)),'sourceEdges':edge(source),'outputEdges':edge(native)})
 draw.text((8,6+i*104),topic,fill='black')
 for j,color in enumerate(['white','black','#727b43']):
  tile=Image.new('RGBA',(64,64),color);tile.alpha_composite(native);sheet.paste(tile.convert('RGB'),(15+j*130,27+i*104))
sheet.save(DOC/'native-review-white-black-olive.png')
(DOC/'proposed-integration-candidates.json').write_text(json.dumps({'schemaVersion':1,'assets':catalog},indent=2)+'\n')
(DOC/'delivery-manifest.json').write_text(json.dumps({'status':'candidate masters, not formally registered','assets':rows},ensure_ascii=False,indent=2)+'\n')
print(json.dumps([{k:r[k] for k in ['topicId','sourceEdges','outputEdges']} for r in rows],indent=2))
