from pathlib import Path
from PIL import Image, ImageDraw, ImageOps
import numpy as np
import hashlib,json,shutil
B=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-props')
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
A=R/'docs/art-rebuild-20261019/approved-v5/narrative-keyprop-audit'
SOURCES={"prop-casualty-register":"/workspace/scratch/84c9fe948755/generated_images/exec-6c1af861-ed50-4c82-9268-96e0511c23f6.png","prop-name-tags":"/workspace/scratch/84c9fe948755/generated_images/exec-25767ee7-26e1-474e-82ef-6ff1cb353eb5.png","prop-evacuation-map":"/workspace/scratch/84c9fe948755/generated_images/exec-86ab8b47-87d7-417a-96c8-ea420a80fe25.png","prop-first-grey-flag":"/workspace/scratch/84c9fe948755/generated_images/exec-6aaf5279-1b64-479c-9414-c6b79b2042f7.png"}
BG={'black':(0,0,0,255),'white':(255,255,255,255),'olive':(100,104,66,255),'dark':(28,33,42,255)}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def stats(im):
 a=np.array(im.getchannel('A')); edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 return dict(size=list(im.size),mode=im.mode,alphaExtrema=[int(a.min()),int(a.max())],transparentPixels=int((a==0).sum()),partialPixels=int(((a>0)&(a<255)).sum()),opaquePixels=int((a==255).sum()),nearOpaquePixels=int((a>=250).sum()),edgeMaxAlpha=int(edge.max()),edgeNonzeroSamples=int((edge>0).sum()),alphaBoundingBox=im.getchannel('A').getbbox(),alpha8BoundingBox=Image.fromarray((a>=8).astype('uint8')*255).getbbox())
audit=json.loads((A/'manifest.json').read_text())
for slug,p in SOURCES.items():
 d=B/slug;q=d/'QA';raw=d/'raw/attempt-01.png';master=d/'master.png';src=Path(p)
 shutil.copyfile(src,raw);shutil.copyfile(src,master)
 im=Image.open(master);assert im.mode=='RGBA';assert im.size==(1254,1254)
 thumbnail=ImageOps.contain(im,(124,124),Image.Resampling.LANCZOS);thumb=Image.new('RGBA',(128,128));thumb.paste(thumbnail,((128-thumbnail.width)//2,(128-thumbnail.height)//2));thumb.save(q/'contain-128.png')
 sheet=Image.new('RGB',(560,160),(215,215,215));draw=ImageDraw.Draw(sheet)
 for i,(name,color) in enumerate(BG.items()):
  comp=Image.new('RGBA',(128,128),color);comp.alpha_composite(thumb);comp=comp.convert('RGB');comp.save(q/f'native128-{name}.png');sheet.paste(comp,(i*140+6,25));draw.text((i*140+6,6),name,fill=(0,0,0))
  full=Image.new('RGBA',im.size,color);full.alpha_composite(im);full.convert('RGB').save(q/f'source-{name}.png')
 sheet.save(q/'native128-review.png')
 record=next(x for x in audit['topics'] if x['topicId']=='C01-'+slug.upper());(d/'reference-audit.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
 ref=Image.open(A/'original-crops'/('c01-'+slug)/'legacy-inset.png').convert('RGB');contact=Image.new('RGB',(1234,647),(230,230,230));draw=ImageDraw.Draw(contact);draw.text((5,6),'Original617px identity reference',fill=(0,0,0));draw.text((622,6),'New master, scaled only for QA',fill=(0,0,0));contact.paste(ref,(0,30));contact.paste(Image.open(q/'source-white.png').resize((617,617),Image.Resampling.LANCZOS),(617,30));contact.save(q/'reference-fidelity.png')
 ss,ts=stats(im),stats(thumb)
 qa={'source':ss,'contain128':ts,'wholeCanvasResize':{'source':list(im.size),'target':[128,128],'fit':[124,124],'padding':[2,2],'crop':None,'unmaskedPaste':True},'sourceBytesUnmodified':True,'alphaCleanup':False,'chromaKey':False,'bboxFit':False,'generatedSource':str(src),'native128Backgrounds':list(BG),'fullSourceBackgrounds':list(BG),'visualReviewRequired':True}
 (q/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
 req=json.loads((d/'request.json').read_text());proof={'topicId':'C01-'+slug.upper(),'tool':'built-in image_gen.imagegen','generatedNativePath':str(src),'selectedAttempt':1,'generationCalls':1,'rejectedAttempts':[],'nativeSize':list(im.size),'generatedNativeSha256':sha(src),'rawSha256':sha(raw),'masterSha256':sha(master),'masterIsByteExactGeneratedSource':sha(src)==sha(raw)==sha(master),'transparentBackgroundRequested':True,'alphaRetainedUnmodified':True,'sourceBoundaryMaxAlpha':ss['edgeMaxAlpha'],'sourceBoundaryNonzeroSamples':ss['edgeNonzeroSamples'],'noAlphaCleanup':True,'noChromaKey':True,'noSourceResizeOrCrop':True,'referencePaths':req['referenced_image_paths'],'references':[{'path':p,'sha256':sha(Path(p))} for p in req['referenced_image_paths']],'originalBoard':record['source'],'sourceProofScope':'Independent native genuine RGBA generation. Original617px RGB image was identity reference only; approved master style reference only. Master/raw are byte-exact generated native output. QA contain128 uses whole canvas124+2px padding and unmasked paste; no bbox fit or normalization.'}
 (d/'sourceproof.json').write_text(json.dumps(proof,indent=2)+'\n');(d/'hash.sha256').write_text(sha(master)+'  master.png\n'+sha(raw)+'  raw/attempt-01.png\n');(d/'raw/rejections.json').write_text('[]\n')
 print(slug,json.dumps(ss),json.dumps(ts))
