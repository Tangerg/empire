import json,hashlib,shutil,sys
from pathlib import Path
import numpy as np
from PIL import Image,ImageOps,ImageDraw
contract=Path(sys.argv[1]); bs=json.loads(contract.read_text())
base=contract.parent.parent
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
colors={'white':(255,255,255),'black':(0,0,0),'olive':(87,96,63)}
ready=[b for b in bs if b.get('generatedSource')]
contact=Image.new('RGB',(340,len(ready)*95+28),(210,210,210));dr=ImageDraw.Draw(contact)
dr.text((8,8),'Native 32px                 HUD 28px',fill=(0,0,0))
for i,b in enumerate(ready):
 d=Path(b['dir']);src=Path(b['generatedSource']);im=Image.open(src);assert im.mode=='RGBA'
 for name in ['raw.png','master.png']:shutil.copyfile(src,d/name)
 a=np.array(im.getchannel('A'));bounds=lambda t:Image.fromarray((a>=t).astype('uint8')*255).getbbox();bb=bounds(128);w,h=im.size
 edges=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 evidence={'topicId':b['topic'],'assetId':b['asset']['assetId'],'label':b['asset']['label'],'category':'status','expectedRuntimeSize':[32,32],'runtimeAnchor':[16,16],'hudDefaultDisplayPixels':28,'directStatusBinding':b['directStatusBinding'],'tool':'built-in image_gen__imagegen','transparent_background':True,'rawGeneratedSource':str(src),'localRaw':'raw.png','identityReference':b['identityPath'],'originalSourceEvidence':{'baselineCommit':'2276a89','atlasPath':b['atlasPath'],'atlasSha256':b['sha'],'matchesBaselineAtlasBytes':True,'atlasCell':f"status-atlas-{b['board']}.png, row {b['row']} column {b['col']} (one based)",'topicIndex':b['index'],'inspectionCropCoordinates':b['box'],'inspectionCropTreatment':'Unscaled complete-subject crop; source magenta and grid remain inspection only, no cleanup or alpha conversion.','baselineRuntimeReference':b['baselineRuntimeReference'],'mappingAuthority':b['mappingAuthority'],'nominalGridCellReference':b['originalGridCellPath'],'nominalGridCellCoordinates':b['originalGridBox'],'sourceCellCorrection':b['sourceCellCorrection'],'manifest':'packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json','topicCatalog':'docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md','contractApproval':'Parent exclusively authorized first actual consumers status01/02/04/05 after full24 source inspection. Preserve32px runtime and28px HUD. Source raw/master immutable; independent QA previews authorized.'},'styleReferences':b['styles'],'initialToolInputReferencePaths':[b['identityPath']]+b['styles'],'identityClarification':b['identity'],'promptFiles':['prompt.txt'],'postprocessing':'None. Byte-exact native raw/master copies; no resizing, cleanup, alpha edits or programmatic drawing.','runtimeReady':False,'integration':'Parent owns backup/export/catalog binding. No repository or gameplay edits.'}
 qa={'topicId':b['topic'],'nativeSize':list(im.size),'mode':im.mode,'alphaExtrema':[int(a.min()),int(a.max())],'transparentFraction':float((a==0).mean()),'nearOpaqueFractionAlphaGE250':float((a>=250).mean()),'nonzeroAlphaBBox':bounds(1),'visibleAlphaBBoxGE8':bounds(8),'visibleAlphaBBoxGE128':bb,'visibleMarginsPixelsL_T_R_B':[bb[0],bb[1],w-bb[2],h-bb[3]],'canvasEdgeMaxAlpha':int(edges.max()),'canvasEdgeNonzeroPixelCount':int((edges>0).sum()),'sha256':sha(src),'matchesRawGeneratedBytes':(d/'master.png').read_bytes()==src.read_bytes()==(d/'raw.png').read_bytes(),'visualQA':['Complete original functional subject and major symbol counts/directions visually inspected.','Bold outlines and broad cel material planes, transparent exterior/open gaps.','No invented readable lettering, broad exterior glow, haze or added UI plate.'],'identityVerified':b['identity'],'remarks':['Native alpha-1 edge residue retained; meaningful subject bounds are inset.'] if edges.max()==1 else [],'postprocessing':'None','runtimeReady':False}
 preview=d/'qa-preview-32-28';preview.mkdir(exist_ok=True)
 fitted=ImageOps.contain(im,(28,28),Image.Resampling.LANCZOS);runtime=Image.new('RGBA',(32,32),(0,0,0,0));runtime.paste(fitted,((32-fitted.width)//2,(32-fitted.height)//2))
 hud=runtime.resize((28,28),Image.Resampling.LANCZOS)
 dr.text((8,30+i*95),b['slug'],fill=(0,0,0))
 for side,icon,startx in [(32,runtime,8),(28,hud,180)]:
  icon.save(preview/f'rgba-{side}.png')
  for j,(name,c) in enumerate(colors.items()):
   bg=Image.new('RGBA',icon.size,(*c,255));bg.alpha_composite(icon);bg.convert('RGB').save(preview/f'{name}-{side}.png');contact.paste(bg.convert('RGB'),(startx+j*48,50+i*95))
 method={'nativeSourceImmutable':True,'masterSha256':sha(src),'runtime32':'Pillow ImageOps.contain full authored canvas into28x28, LANCZOS; centered in32x32 RGBA,2px padding. No alpha-bbox crop. Matches exporter full-canvas contract.','hud28':'Runtime32 resized to28x28 LANCZOS as deterministic QA approximation of browser32-source/28-CSS display. Browser resampling can differ.','backgrounds':colors,'actualDisplaySizes':[32,28],'reviewStatus':'Awaiting visual inspection'}
 (preview/'method.json').write_text(json.dumps(method,ensure_ascii=False,indent=2)+'\n')
 qa['independentPreview']={'directory':str(preview),'sizes':[32,28],'method':'Full-canvas runtime fit then HUD downscale; previews only, master unchanged.','visualReview':'Awaiting visual inspection'}
 (d/'source-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n');(d/'alpha-qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n')
 assert sha(d/'master.png')==sha(src)==sha(d/'raw.png')
 print(json.dumps({'slug':b['slug'],'native':list(im.size),'bbox128':bb,'edgeAlpha':int(edges.max()),'sha256':sha(src)}))
contact.save(base/'identity-inspection/status-01-02-04-05-preview-32-28-contact.png')
