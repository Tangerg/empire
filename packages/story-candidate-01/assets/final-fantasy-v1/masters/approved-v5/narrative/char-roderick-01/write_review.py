from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib,shutil
b=Path(__file__).parent;im=Image.open(b/'master.png').convert('RGBA');a=np.array(im)[:,:,3];q=json.loads((b/'alpha-qa.json').read_text());req=json.loads((b/'request.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
assert sha(b/'master.png')==sha(b/'raw.png')==sha(q['nativeSource'])==q['sha256'];assert sha(b/'prompt.txt')==req['promptSha256'];assert q['perimeterCountAlphaGreaterThan1']==0
for p,h in req['referencedImageSha256'].items():assert sha(p)==h
orig=json.loads((b/'attempt-01/request.json').read_text())
for p,h in orig['referencedImageSha256'].items():assert sha(p)==h
probes=[]
for name,cx,cy in [('between-boots',504,1383),('right-glove-to-tunic',266,770),('empty-sheath-to-boot',185,1242)]:
 choices=[]
 for y in range(cy-25,cy+26):
  for x in range(cx-25,cx+26):
   v=a[y-3:y+4,x-3:x+4]
   if v.shape==(7,7) and not v.any():choices.append(((x-cx)**2+(y-cy)**2,x,y))
 assert choices,name;_,x,y=min(choices);probes.append({'name':name,'rectangle':[x-3,y-3,x+4,y+4],'maxAlpha':0,'zeroPixels':49})
empty=[]
for name,rect in [('upper-left-outside',[0,0,250,300]),('upper-right-outside',[800,0,1024,300]),('between-boot-bottoms',[460,1430,560,1500])]:
 x0,y0,x1,y1=rect;v=a[y0:y1,x0:x1];empty.append({'name':name,'rectangle':rect,'maxAlpha':int(v.max()),'medianAlpha':float(np.median(v)),'zeroPixels':int((v==0).sum()),'pixelCount':int(v.size)})
bbox=q['bboxAlphaGreaterThan127'];margins={'left':bbox[0],'top':bbox[1],'right':im.width-bbox[2],'bottom':im.height-bbox[3]}
q.update({'selectedAttempt':'attempt-02','nativeCanvasRatio':'2:3','semanticReview':'Same named elder face with exposed swept gray hair, full gray beard/moustache, strong dark brows, broad matte gray steel armor and blue-gray scarf/tabard as the approved mounted quartet. Dismounted fullbody stance uses original named portrait vocabulary: one straight sword held in right hand, original plain round shield on left arm. Extra short sheathed weapon from attempt01 removed; ordinary brown flap pouch restored; a single long empty brown scabbard remains naturally partly obscured behind the held sword/hand. No horse needed for this separate portrait; mounted rules unchanged.','revisionReview':'Targeted extra-equipment removal succeeds; original and correction native bytes/prompts preserved. Face, hands, shield and standing pose remain consistent; long scabbard is slightly extended in correction, disclosed as native redraw.','negativeSpaceProbes':probes,'emptyExteriorSamples':empty,'visibleMarginsSourcePixels':margins,'marginCaveat':'Horizontal visible margins28/24px are narrower than prompt50–75 target, but entire figure/roundshield/held-sword pommel and blade tips are complete with native perimeterA>1 count0. No trim/fit used.','previewTransparencyCaveat':'Tool inline preview may show hidden RGB haze, but actual white/black alpha composites and exterior alpha samples show clean transparency. Native hidden RGB/alpha1 samples are retained; no cleanup.','status':'Transparent named portrait source inspected; coordinator card/hero consistency review pending','sourcePostprocessing':'None. Raw/master/native bytes identical. Composites and whole-canvas downscale previews are diagnostic only.'})
# Full authored-canvas fit, never trim to silhouette.
proof=Image.new('RGB',(768,488),(220,220,220));draw=ImageDraw.Draw(proof);transforms={}
for size in [(192,224),(92,112)]:
 w,h=size;scale=min(w/im.width,h/im.height);tx=(w-im.width*scale)/2;ty=(h-im.height*scale)/2;full=[tx,ty,tx+im.width*scale,ty+im.height*scale];bounds=[tx+bbox[0]*scale,ty+bbox[1]*scale,tx+bbox[2]*scale,ty+bbox[3]*scale]
 tile=im.transform(size,Image.Transform.AFFINE,(1/scale,0,-tx/scale,0,1/scale,-ty/scale),resample=Image.Resampling.BICUBIC,fillcolor=(0,0,0,0));tile.save(b/f'qa-card-{w}x{h}-fullcanvas-rgba.png');transforms[f'{w}x{h}']={'scale':scale,'offset':[tx,ty],'wholeCanvasBounds':full,'visibleBounds':bounds}
 for idx,bg in enumerate(['white','black']):
  comp=Image.new('RGBA',size,bg);comp.alpha_composite(tile);comp.convert('RGB').save(b/f'qa-card-{w}x{h}-{bg}.png')
  if size==(192,224):proof.paste(comp.convert('RGB').resize((384,448),Image.Resampling.NEAREST),(idx*384,40));draw.text((idx*384+6,7),f'192x224 full canvas / {bg} / 2x',fill='black')
proof.save(b/'qa-card-fullcanvas-2x.png');q['wholeCanvasPreviewTransforms']=transforms
manifest={'artifactKind':'named-character-transparent-narrative-master','cardTopicId':'C01-CHAR-RODERICK-01','unitType':'c01.roderick','status':q['status'],'master':str(b/'master.png'),'raw':str(b/'raw.png'),'nativeSize':list(im.size),'nativeMode':im.mode,'sha256':q['sha256'],'generationSource':q['nativeSource'],'exactPrompt':str(b/'prompt.txt'),'request':str(b/'request.json'),'alphaQA':str(b/'alpha-qa.json'),'sourcePostprocessing':'none','selectedAttempt':'attempt-02','supersededAttempts':[{'directory':'attempt-01','reason':'Unintended extra short sheathed weapon at upper hip. Native imagegen edit removes it and restores plain pouch.'}],'identitySources':orig['referencedImageSha256'],'promptChain':['attempt-01/prompt.txt','attempt-02/prompt.txt'],'battleSourceEvidence':'../../hero-appearances/roderick/source-evidence.json','battleQuartetManifest':'../../hero-appearances/roderick/bundle-manifest.json','originalNamedIdentity':'Exact original top-right named board crop [632,5,1249,622]; original card C01-CHAR-RODERICK-01. No numerical age invented.','cardCompositionContract':'Preserve entire2:3transparent native canvas. Fit uniformly into192x224 and92x112. Background is a separately authored layer, not alpha-extracted or generated into this person master.','wholeCanvasPreviewTransforms':transforms,'semanticReview':q['semanticReview'],'negativeSpaceProbes':probes}
for name,obj in [('alpha-qa.json',q),('bundle-manifest.json',manifest)]:
 p=b/name
 if p.exists():
  old=json.loads(p.read_text())
  if 'coordinatorNarrativeApproval' in old:obj['coordinatorNarrativeApproval']=old['coordinatorNarrativeApproval'];obj['status']=old.get('status',obj['status'])
 p.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
shutil.copy2(b/'alpha-qa.json',b/'attempt-02/alpha-qa.json')
(b/'source-review.md').write_text('# Roderick independent transparent narrative portrait\n\n'+q['status']+'\n\n'+q['semanticReview']+'\n\nThe selected native is1024×1536RGBA, sha256 '+q['sha256']+'. Original/raw/master bytes agree. Exact prompts and reference hashes verified. Attempt01 extra short sheathed weapon is preserved, not silently replaced in provenance. Attempt02 removes the extra pommel/short sheath and restores the plain leather pouch; held sword and one empty long sheath remain.\n\nStrict native perimeter gate passes:0edge pixels alpha>1. Full visible bounds'+str(bbox)+', margins'+str(margins)+'. All3seven-by-seven negative-space probes have49zero-alpha pixels. Native alpha1/hidden RGB samples remain untouched; actual black/white composites are clean.\n\nCard preview uses the ENTIRE native canvas, uniform scale224/1536 and offset[21.3333333333,0] into192×224. Smaller92×112 preview likewise uses full canvas. These are QA files only, not newly authored card backgrounds or portrait masters. Parent owns independent background composition, actual dialogue/roster/HUD review, binding and tests.\n\nMounted battle representation is separately based on actual mounted/heavy lance rules and the original cavalry horse construction. The original named portrait is dismounted with sword/roundshield. This portrait does not redefine gameplay, invent an original personal horse source, remap generic heavy-knight, or add canonical topic count.\n')
print(json.dumps({'nativeSha256':q['sha256'],'perimeterGate':'PASS','negativeSpaceProbes':probes,'visibleMargins':margins,'wholeCanvasPreviewTransforms':transforms,'integrity':'PASS'},indent=2))
