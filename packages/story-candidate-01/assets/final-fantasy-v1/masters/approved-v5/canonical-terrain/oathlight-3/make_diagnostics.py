from PIL import Image,ImageDraw
from pathlib import Path
import json,hashlib,sys,numpy as np
B=Path('/workspace/shared/empire-art-rebuild/approved-v5/canonical-terrain')
windows=[[0,0,627,627],[627,0,1254,627],[0,627,627,1254],[627,627,1254,1254]]
def mask(s,m):
 a=Image.new('L',(s,s),0);d=ImageDraw.Draw(a)
 if s==32:c=[10,10,21,21];r=4;arms=[[12,0,19,16],[16,12,31,19],[12,16,19,31],[0,12,16,19]]
 else:c=[20,20,43,43];r=8;arms=[[24,0,39,32],[32,24,63,39],[24,32,39,63],[0,24,32,39]]
 d.rounded_rectangle(c,radius=r,fill=255)
 for k,box in enumerate(arms):
  if m&(1<<k):d.rectangle(box,fill=255)
 return a
for st in sys.argv[1:]:
 i=int(st);windows=([[0,38,627,665],[627,38,1254,665],[0,627,627,1254],[627,627,1254,1254]] if i==3 else [[0,0,627,627],[627,0,1254,627],[0,627,627,1254],[627,627,1254,1254]]);p=B/f'oathlight-{i}';im=Image.open(p/'master.png');proof=Image.new('RGB',(768,576),(90,90,90))
 for c,w in enumerate(windows):
  crop=im.crop(w)
  proof.paste(crop.resize((192,192),Image.Resampling.LANCZOS),(c*192,0))
  for row,side in [(1,32),(2,64)]:proof.paste(crop.resize((side,side),Image.Resampling.LANCZOS).resize((192,192),Image.Resampling.NEAREST),(c*192,row*192))
 proof.save(p/'source-crop-32-64-proof.png')
 if i in (2,3):
  for size in (32,64):
   tile=im.crop(windows[0]).resize((size,size),Image.Resampling.LANCZOS);board=Image.new('RGB',(1024,256),(40,40,40));data=[]
   for m in range(16):
    a=mask(size,m);rgba=tile.convert('RGBA');rgba.putalpha(a);bg=Image.new('RGB',(size,size),(45,45,53));bg.paste(tile,(0,0),a);board.paste(bg.resize((128,128),Image.Resampling.NEAREST),((m%8)*128,(m//8)*128));data.append({'mask':m,'opaque':sum(v==255 for v in a.getdata()),'transparent':sum(v==0 for v in a.getdata())})
   board.save(p/f'fixed-mask-{size}-proof.png')
   (p/f'fixed-mask-{size}-counts.json').write_text(json.dumps(data,indent=2)+'\n')
 x=np.asarray(im).astype(float)
 qa={'topicId':f'C01-TERRAIN-OATHLIGHT-{i}','status':'raw-candidate-reviewed; final integrator export pending','authoredMaterialCount':1,'builtInImagegenCalls':2 if i in(2,3) else 1,'method':'Built-in image_gen; selected raw copied byte-for-byte with no pixel transformation','master':'master.png','sourceSHA256':hashlib.sha256((p/'master.png').read_bytes()).hexdigest(),'dimensions':list(im.size),'mode':im.mode,'fullyOpaque':im.mode=='RGB','rawPixelsPreserved':True,'prompt':'prompt.txt','request':'request.json','selectedPrompt':'prompt-selected.txt' if i in(2,3) else 'prompt.txt','selectedRequest':'request-selected.json' if i in(2,3) else 'request.json','references':'references.json','sourceAudit':'source-audit.json','sourceMapping':'source-mapping.json','tileMode':'nesw-16' if i in(2,3) else 'variants','derivedCellCount':16 if i in(2,3) else 4,'diagnosticNativeQA':{'proof':'source-crop-32-64-proof.png','windowsXYXY':windows,'rowMeaning':['627px source square reduced to192px','32px LANCZOS crop enlarged6x nearest','64px LANCZOS crop enlarged3x nearest'],'edits':'QA-only crop, resize and composite; master unchanged','finalExportWindowsApproved':False},'fixedMaskDiagnostic':{'performed':i in(2,3),'window':windows[0] if i in(2,3) else None,'oldMaskGeometryUnchanged':True,'notFinalExport':True},'rawOppositeEdgeMeasurements':{'eastWestExact':bool(np.array_equal(x[:,0],x[:,-1])),'northSouthExact':bool(np.array_equal(x[0],x[-1])),'eastWestMeanAbsoluteRGBDifference':float(np.abs(x[:,0]-x[:,-1]).mean()),'northSouthMeanAbsoluteRGBDifference':float(np.abs(x[0]-x[-1]).mean())},'export':{'performed':False,'alphaAndSeamQA':'pending integrator','formalCatalog':'pending','liveMapAcceptance':'not claimed; catalog-only canonical terrain per source audit'}}
 (p/'qa.json').write_text(json.dumps(qa,indent=2)+'\n');print(i,im.size,im.mode)
