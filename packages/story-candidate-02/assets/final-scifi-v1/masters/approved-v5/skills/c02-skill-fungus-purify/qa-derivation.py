from pathlib import Path
from PIL import Image,ImageOps,ImageFilter,ImageDraw
import json,hashlib,sys
O=Path(__file__).parent
for slug in sys.argv[1:]:
 d=O/slug;im=Image.open(d/'master.png').convert('RGBA');w,h=im.size;a=im.getchannel('A'); hist=a.histogram();bbox=a.point(lambda p:255 if p>=8 else 0).getbbox()
 edge=[a.getpixel((x,y)) for x in range(w) for y in [0,h-1]]+[a.getpixel((x,y)) for y in range(h) for x in [0,w-1]]
 inner=a.point(lambda p:255 if p>=8 else 0).filter(ImageFilter.MinFilter(17)); vals=[p for p,m in zip(a.getdata(),inner.getdata()) if m]; ermin=min(vals) if vals else None
 qa=d/'diagnostic-native48';qa.mkdir(exist_ok=True)
 # Deterministic registered static-icon geometry: fit whole canvas in44² with2px margin. No bbox crop or alpha edit.
 small=ImageOps.contain(im,(44,44),Image.Resampling.LANCZOS);out=Image.new('RGBA',(48,48));out.alpha_composite(small,((48-small.width)//2,(48-small.height)//2));out.save(qa/'runtime48-diagnostic.png')
 oa=out.getchannel('A');oh=oa.histogram();oe=[oa.getpixel((x,y)) for x in range(48) for y in [0,47]]+[oa.getpixel((x,y)) for y in range(48) for x in [0,47]]
 strip=Image.new('RGB',(144,48))
 for j,(name,color) in enumerate([('white','#ffffff'),('black','#000000'),('olive','#68704b')]):
  bg=Image.new('RGBA',(48,48),color);bg.alpha_composite(out);bg.convert('RGB').save(qa/f'native48-on-{name}.png');strip.paste(bg.convert('RGB'),(j*48,0))
 strip.save(qa/'native48-white-black-olive.png');strip.resize((864,288),Image.Resampling.NEAREST).save(qa/'native48-white-black-olive-6x-nearest.png')
 for name,color in [('white','#ffffff'),('black','#000000'),('olive','#68704b')]:
  bg=Image.new('RGBA',im.size,color);bg.alpha_composite(im);bg.resize((512,512),Image.Resampling.LANCZOS).convert('RGB').save(d/f'qa-source512-on-{name}.png')
 rawhash=hashlib.sha256((d/'raw.png').read_bytes()).hexdigest();masterhash=hashlib.sha256((d/'master.png').read_bytes()).hexdigest()
 r={'topicId':slug,'source':'master.png','nativeSize':[w,h],'mode':Image.open(d/'master.png').mode,'alphaExtrema':list(a.getextrema()),'zeroAlphaPixels':hist[0],'partialAlphaPixels':sum(hist[1:255]),'opaquePixels':hist[255],'canvasEdgeMaxAlpha':max(edge),'canvasEdgeAlpha2PlusCount':sum(x>=2 for x in edge),'visibleAlphaBBoxGE8':list(bbox),'visibleMarginsPixelsL_T_R_B':[bbox[0],bbox[1],w-bbox[2],h-bbox[3]],'nearOpaquePixelCountGE240':sum(hist[240:]),'diagnostic8pxErosion':{'minAlpha':ermin,'pixelsBelow240':sum(v<240 for v in vals),'notAcceptanceGate':True},'actual48EdgeMaxAlpha':max(oe),'actual48NearOpaquePixelsGE240':sum(oh[240:]),'formalSourceContractPass':min(a.getextrema())==0 and max(edge)<=1 and sum(hist[240:])>0 and max(oe)<=1,'derivation':'Diagnostic only; full-source Lanczos contain44x44 centered48x48 with2px padding. Native source unchanged. White/black/olive composites for inspection only.','sha256':masterhash,'matchesRawGeneratedBytes':rawhash==masterhash,'postprocessing':'No source pixel editing, crop, recolor, alpha keying or cleanup. Separate QA-only derivatives.','runtimeReady':False,'formalRuntimeIntegration':False,'liveGameVerified':False}
 (d/'alpha-qa.json').write_text(json.dumps(r,indent=2)+'\n');print(slug,json.dumps(r))
