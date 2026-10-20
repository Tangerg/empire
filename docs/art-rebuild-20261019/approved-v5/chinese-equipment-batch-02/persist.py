import json,hashlib,shutil,sys
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[4];sys.path.insert(0,str(ROOT/'tools/art'))
from export_overrides import fit_canvas,validate_alpha,plan
DOC=Path(__file__).resolve().parent;PACK=ROOT/'packages/story-candidate-03/assets/final-ancient-china-v1'
spec=json.loads((DOC/'generation-spec.json').read_text());calls=['ef9a3847-dba5-45ec-9e5a-3014ae9a13b6','1abccd47-20e3-4791-b896-6317a91b9106','7b49967e-5266-449c-9fc0-67da6547e66b','8eb0c822-e430-4a56-85e8-63b9b6068541']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assets={a['topicId']:a for a in json.loads((PACK/'manifest-final-ancient-china-v1.json').read_text())['assets']}
cat={};rows=[];sheet=Image.new('RGB',(380,340),'#dddddd');draw=ImageDraw.Draw(sheet);full=Image.new('RGBA',(2508,2508),'#727b43')
for i,(a,call) in enumerate(zip(spec['assets'],calls)):
 topic=a['topicId'];canonical=assets[topic];dest=PACK/'masters/approved-v5/equipment'/topic;dest.mkdir(parents=True,exist_ok=True)
 raw=ROOT.parent/'generated_images'/('exec-'+call+'.png')
 for f in ['raw.png','master.png']:shutil.copyfile(raw,dest/f)
 (dest/'prompt.txt').write_text(spec['shared']+a['subject']+'\n')
 source=Image.open(dest/'master.png');qa=validate_alpha(source,topic);native,contract=fit_canvas(source,(48,48),2);native.save(DOC/(topic+'-48.png'))
 original=PACK/canonical['png'];shutil.copyfile(original,DOC/(topic+'-legacy-native.png'))
 evidence={'topicId':topic,'assetId':canonical['assetId'],'label':canonical['label'],'tool':'built-in image_gen.imagegen','transparent_background':True,'rawGeneratedSource':str(raw),'sha256':sha(raw),'promptFile':'prompt.txt','references':[{'path':p,'sha256':sha(ROOT/p)} for p in spec['references']],'identityReconstruction':a['identity'],'identityEvidence':['docs/story-candidates/candidate-03/art-assets/02-asset-scope.md','docs/story-candidates/candidate-03/art-assets/07-topic-catalog.md','packages/story-candidate-03/assets/final-ancient-china-v1/ART-DIRECTION.md'],'legacyRuntime':{'path':str(original.relative_to(ROOT)),'sha256':sha(original),'preservedCopy':str((DOC/(topic+'-legacy-native.png')).relative_to(ROOT))},'sourceSize':list(source.size),'sourceAlpha':qa,'exportContract':contract,'outputAlpha':validate_alpha(native,'native48'),'postprocessing':'None. Raw/master byte-identical native generation. Only review exports use existing whole-canvas contain; no crop or pixel cleanup.','registered':False}
 (dest/'source-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n');(dest/'alpha-qa.json').write_text(json.dumps({'source':qa,'output':evidence['outputAlpha']},indent=2)+'\n')
 cat[topic]={'source':str((dest/'master.png').relative_to(ROOT)),'sha256':sha(raw)};rows.append({**canonical,**cat[topic],'evidence':str((dest/'source-evidence.json').relative_to(ROOT))})
 draw.text((8,5+i*84),topic,fill='black')
 for j,color in enumerate(['white','black','#727b43']):
  tile=Image.new('RGBA',(48,48),color);tile.alpha_composite(native);sheet.paste(tile.convert('RGB'),(15+j*122,25+i*84))
 full.alpha_composite(source,(i%2*1254,i//2*1254))
sheet.save(DOC/'native-review-white-black-olive.png');full.convert('RGB').save(DOC/'full-source-review.png')
catalog=DOC/'integration-candidates.json';catalog.write_text(json.dumps({'schemaVersion':1,'assets':cat},indent=2)+'\n')
planned=plan(ROOT,catalog);(DOC/'export-plan-qa.json').write_text(json.dumps({'count':len(planned),'assets':[{'topicId':t,'provenance':p,'target':str(target.relative_to(ROOT)),'size':list(im.size)} for t,p,target,im in planned]},indent=2)+'\n')
(DOC/'delivery-manifest.json').write_text(json.dumps({'status':'integration candidates only','assets':rows},ensure_ascii=False,indent=2)+'\n')
print('Saved',len(rows),'sources; isolated export plan count',len(planned))
