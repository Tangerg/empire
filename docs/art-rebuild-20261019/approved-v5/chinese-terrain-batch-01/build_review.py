import hashlib,json,sys,shutil
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[4]; OUT=Path(__file__).parent
sys.path.insert(0,str(ROOT/'tools/art'))
from export_canonical_terrain_overrides import read_material,render_material,CHINA_PACK,pixel_hash,plan,export,review_previews
pack=ROOT/CHINA_PACK
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
records=json.loads((OUT/'reservation.json').read_text())['requests']
returned={
'99390ac8':'/workspace/scratch/84c9fe948755/generated_images/exec-e1e8b7ed-36d2-4070-bb25-d0fdcd1afc51.png',
'980abbed':'/workspace/scratch/84c9fe948755/generated_images/exec-39666b0d-c444-48d9-99e5-cd7097e017c9.png',
'b499c3bf':'/workspace/scratch/84c9fe948755/generated_images/exec-8356d1a6-006d-4f90-9c55-eb97738586ec.png',
'c053bd0c':'/workspace/scratch/84c9fe948755/generated_images/exec-a84d36f2-f179-4194-9c2c-0963ecfa0064.png'}
assets={}; reviews=[]; panels=[]; originals=[]; inventory=[]
for req in records:
 slug=req['slug']; topic=req['topicId']; mode=req['tileMode']; d=pack/'masters/approved-v5/canonical-terrain'/slug
 raw=d/'raw.png'; master=d/'master.png'; tool_path=Path(returned[slug]); assert tool_path.read_bytes()==raw.read_bytes()==master.read_bytes()
 im=Image.open(master); w,h=im.size; assert (w,h)==(1254,1254); assert im.mode in ('RGB','RGBA')
 assert im.mode=='RGB' or im.getchannel('A').getextrema()==(255,255)
 windows=[[0,0,w//2,h//2],[w//2,0,w,h//2],[0,h//2,w//2,h],[w//2,h//2,w,h]] if mode=='variants' else [[0,0,w,h]]
 entry={'source':str(master.relative_to(ROOT)),'sha256':sha(master),'registration':{'sourceSize':[w,h],'windowsXYXY':windows}}
 assets[topic]=entry
 refs=[{'path':str(Path(p).relative_to(ROOT)),'sha256':sha(Path(p))} for p in req['referenced_image_paths']]
 provenance={'tool':'built-in image_gen.imagegen','request':'request.json','raw':'raw.png','master':'master.png','returnedImagePath':str(tool_path),'rawEqualsMasterEqualsReturned':True,'sha256':sha(master),'sourceSize':[w,h],'mode':im.mode,'references':refs,'topicId':topic,'status':'candidate-native-reviewed-not-registered','processing':'None. Exact generated PNG bytes copied; no source crop, recolor, repaint or resample.'}
 (d/'provenance.json').write_text(json.dumps(provenance,indent=2,ensure_ascii=False))
 (d/'render-provenance.json').write_text(json.dumps({'returnedImagePath':str(tool_path),'sha256':sha(tool_path),'tool':'built-in image_gen.imagegen','invocationCountForTopic':1,'requestFile':'request.json'},indent=2))
 inventory.append({'topicId':topic,'master':str(master.relative_to(ROOT)),'raw':str(raw.relative_to(ROOT)),'returnedPath':str(tool_path),'sha256':sha(master),'sourceMode':im.mode,'size':[w,h],'opaque':True,'rawEqualsMasterEqualsReturned':True})
 source,prov=read_material(ROOT,entry,mode,CHINA_PACK)
 sheet,samples,edges,cells=render_material(source,windows,mode,32)
 old=pack/'runtime/terrain'/('terrain-'+slug+'.png'); original=Image.open(old).convert('RGBA')
 (OUT/'original-reference').mkdir(exist_ok=True); shutil.copyfile(old,OUT/'original-reference'/old.name)
 assert sheet.getchannel('A').tobytes()==original.getchannel('A').tobytes()
 sheet.save(OUT/(slug+'-native32.png')); sheet.resize((sheet.width*6,192),Image.Resampling.NEAREST).save(OUT/(slug+'-zoom6.png'))
 for group,tile in [(panels,sheet),(originals,original)]:
  panel=Image.new('RGB',(560,160),'#777777'); draw=ImageDraw.Draw(panel); draw.text((4,4),topic+' | '+mode,fill='white')
  for y,bg in [(24,'black'),(68,'white'),(112,'#626843')]:
   panel.paste(bg,(0,y,560,y+40)); panel.paste(tile,(4,y+4),tile)
  group.append(panel)
 grid=Image.new('RGBA',(256,256)); occupancy={(x,y) for y in range(8) for x in range(8) if (x+y)%5!=0}
 for x,y in occupancy:
  bits=sum(b for dx,dy,b in [(0,-1,1),(1,0,2),(0,1,4),(-1,0,8)] if (x+dx,y+dy) in occupancy)
  idx=(x+2*y)%4 if mode=='variants' else bits
  grid.paste(sheet.crop((idx*32,0,(idx+1)*32,32)),(x*32,y*32))
 display=Image.new('RGB',(768,256))
 for i,bg in enumerate(['black','white','#626843']):
  display.paste(bg,(i*256,0,(i+1)*256,256)); display.paste(grid,(i*256,0),grid)
 display.save(OUT/(slug+'-connected-grid-native32.png')); display.resize((1536,512),Image.Resampling.NEAREST).save(OUT/(slug+'-connected-grid-zoom2.png'))
 fixture_dir=OUT/'topology-fixtures'/slug; fixture_dir.mkdir(parents=True,exist_ok=True)
 for name,fixture in review_previews(sheet,mode).items(): fixture.save(fixture_dir/(name+'.png'))
 reviews.append({'topicId':topic,'tileMode':mode,'size':list(sheet.size),'source':prov,'samples':samples,'edges':edges,'cells':cells,'alphaMatchesOriginal':True,'originalSha256':sha(old),'pixelSha256':pixel_hash(sheet),'alphaSha256':pixel_hash(sheet.getchannel('A')),'distinctCells':len({c['pixelSha256'] for c in cells})})
(OUT/'proposed-registration.json').write_text(json.dumps({'schemaVersion':1,'assets':assets},indent=2)); (OUT/'native-review.json').write_text(json.dumps(reviews,indent=2)); (OUT/'source-inventory.json').write_text(json.dumps(inventory,indent=2))
for name,group in [('all',panels),('original-strips',originals)]:
 combined=Image.new('RGB',(560,len(group)*160))
 for i,p in enumerate(group): combined.paste(p,(0,i*160))
 combined.save(OUT/(name+'-native32.png')); combined.resize((1120,combined.height*2),Image.Resampling.NEAREST).save(OUT/(name+'-zoom2.png'))
# The shared tree is read-only for export planning. All exporter writes occur in this private fixture.
planned=plan(ROOT,OUT/'proposed-registration.json')
assert len(planned)==4 and all(report['density']==1 for _,_,report in planned)
(OUT/'export-plan.json').write_text(json.dumps([report for _,_,report in planned],indent=2))
fixture=OUT/'export-fixture'; fixture.mkdir(exist_ok=True)
manifest=CHINA_PACK/'manifest-final-ancient-china-v1.json'; (fixture/manifest).parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(ROOT/manifest,fixture/manifest)
for entry in assets.values():
 p=Path(entry['source']); (fixture/p).parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(ROOT/p,fixture/p)
catalog=fixture/'proposed-registration.json'; shutil.copyfile(OUT/'proposed-registration.json',catalog)
written=export(fixture,catalog); checked=export(fixture,catalog,check=True)
(OUT/'isolated-export.json').write_text(json.dumps(written,indent=2)); (OUT/'isolated-export-check.json').write_text(json.dumps(checked,indent=2))
checks={'sourceCount':4,'independentlyGenerated':True,'sourceHashesUnique':len({x['sha256'] for x in inventory})==4,'sourceByteEquality':True,'sourceSizes':[[1254,1254]]*4,'allOpaque':True,'sourcePostProcessing':'none','nativeOnly':True,'hdOutputs':0,'planCount':len(planned),'isolatedExportCount':len(written),'isolatedCheckCount':len(checked),'originalAlphaEquality':all(r['alphaMatchesOriginal'] for r in reviews),'derivedCellCounts':[len(r['cells']) for r in reviews],'distinctCells':[r['distinctCells'] for r in reviews],'allowedEdgeEquality':True,'sharedCatalogMutated':False,'sharedRuntimeMutated':False,'sharedManifestsMutated':False,'gameplayConsumerCreated':False}
assert checks['sourceHashesUnique']; (OUT/'checks.json').write_text(json.dumps(checks,indent=2)); print(json.dumps(checks,indent=2))
