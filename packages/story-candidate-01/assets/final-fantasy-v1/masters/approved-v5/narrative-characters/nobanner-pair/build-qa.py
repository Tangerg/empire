from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont,ImageFilter
import numpy as np
import json,hashlib,math
BASE=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-characters')
PAIR=BASE/'nobanner-pair';PAIR.mkdir(exist_ok=True)
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',16)
small=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',12)
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def fit(im,size,pos=(.5,.18)):
 s=max(size[0]/im.width,size[1]/im.height);sw=size[0]/s;sh=size[1]/s;x=(im.width-sw)*pos[0];y=(im.height-sh)*pos[1]
 return im.resize(size,Image.Resampling.LANCZOS,box=(x,y,x+sw,y+sh))
def sourceqa(im):
 a=im.getchannel('A');hist=a.histogram();edge=[a.getpixel((x,y)) for y in (0,im.height-1) for x in range(im.width)]+[a.getpixel((x,y)) for x in (0,im.width-1) for y in range(1,im.height-1)]
 ar=np.array(a);inside=np.array(a.filter(ImageFilter.MinFilter(7)))>16
 return dict(mode=im.mode,size=list(im.size),alphaExtrema=a.getextrema(),transparentPixels=hist[0],transparentFraction=hist[0]/(im.width*im.height),nearOpaquePixels=sum(hist[240:]),partialAlphaPixels16to239=sum(hist[16:240]),partialAlphaHistogram=hist,edgeMaxAlpha=max(edge),edgeNonzeroPixels=sum(v>0 for v in edge),edgePixelsAbove1=sum(v>1 for v in edge),alphaAbove1BoundsDiagnosticOnly=a.point(lambda x:255 if x>1 else 0).getbbox(),partialAlphaAwayFromExteriorAA={'alphaRange':[32,223],'pixelsWith7x7NeighborhoodAlphaAbove16':int(np.sum((ar>=32)&(ar<=223)&inside)),'meaning':'Diagnostic count of broad authored partial-alpha regions beyond one-pixel edge antialiasing; no source mutation'})
variants=Image.new('RGB',(1120,670),'#ebe9e5');draw=ImageDraw.Draw(variants);draw.text((16,8),'Offline placement trial: whole square source; no source crop / no live consumer',fill='#222',font=font)
summary=[]
for n in (1,2):
 d=BASE/f'char-nobanner-{n:02}';im=Image.open(d/'master.png');bg=Image.open(d/'references/independent-card-background.png')
 qa=sourceqa(im);assert qa['alphaExtrema'][0]==0 and qa['alphaExtrema'][1]>=240 and qa['edgeMaxAlpha']<=1
 (d/'source-alpha-qa.json').write_text(json.dumps(qa,indent=2))
 im.getchannel('A').save(d/'qa-native-alpha.png')
 for j,w in enumerate((184,176,168)):
  hei=round(im.height*w/im.width);origin=((192-w)//2,(224-hei)//2)
  card=ImageOps.fit(bg.convert('RGBA'),(192,224),Image.Resampling.LANCZOS);card.alpha_composite(im.resize((w,hei),Image.Resampling.LANCZOS),origin)
  mobile=fit(card,(64,88)); hud=fit(card,(112,112),(.5,.5)).crop((8,0,104,112));rost=fit(card,(44,44))
  x=20+j*365;y=45+(n-1)*310
  draw.text((x,y),f'0{n}: width{w} origin{origin}',fill='#222',font=font)
  variants.paste(card.convert('RGB'),(x,y+27));variants.paste(mobile.convert('RGB'),(x+205,y+27));variants.paste(hud.convert('RGB'),(x+205,y+125));variants.paste(rost.convert('RGB'),(x+285,y+27))
  if w==168:
   card.save(d/'card-preview.png')
   for name,size in [('desktop-dialogue',(92,112)),('mobile-dialogue',(64,88)),('roster',(44,44))]:fit(card,size).save(d/f'qa-ui-{name}.png')
   hud.save(d/'qa-ui-hud.png')
   for color,nm in [('white','white'),('black','black')]:
    comp=Image.new('RGBA',im.size,color);comp.alpha_composite(im);comp.convert('RGB').save(d/f'qa-native-{nm}.png')
   comp=Image.new('RGBA',im.size,'#dadada');cd=ImageDraw.Draw(comp)
   for y0 in range(0,im.height,40):
    for x0 in range(0,im.width,40):
     if (x0//40+y0//40)%2:cd.rectangle((x0,y0,x0+39,y0+39),fill='#bcbcbc')
   comp.alpha_composite(im);comp.convert('RGB').save(d/'qa-native-checker.png')
   contract={'topicId':f'C01-CHAR-NOBANNER-{n:02}','kind':'character-card-composite-v1','selectedPlacement':{'renderWidth':w,'origin':list(origin)},'wholeCanvasRenderSize':[w,hei],'nativeSourceSize':list(im.size),'canonicalCardSize':[192,224],'background':{'path':str(d/'references/independent-card-background.png'),'sha256':h(d/'references/independent-card-background.png'),'sourceSize':list(bg.size),'cropBoxXYXY':[(bg.width-bg.height*192/224)/2,0,(bg.width+bg.height*192/224)/2,bg.height],'operation':'independent centered cover'},'sourceCropped':False,'alphaBBoxUsedForFit':False,'registrationStatus':'proposal-only-not-registered','consumerStatus':'catalog-only-no-selected-consumer','liveGameVerified':False,'modeledUiCrops':{'desktopDialogue':{'size':[92,112],'fit':'cover','position':[.5,.18]},'mobileDialogue':{'size':[64,88],'fit':'cover','position':[.5,.18]},'roster':{'size':[44,44],'fit':'cover','position':[.5,.18]},'hud':{'size':[96,112],'viewport':[112,112],'x':-8,'y':0,'fit':'xMidYMid slice'}}}
   (d/'proposed-registration.json').write_text(json.dumps(contract,indent=2));summary.append(contract)
variants.save(PAIR/'qa-placement-variants.png')
# Native card-size + enlarged modeled crops are explicitly diagnostic, never image sources.
sheet=Image.new('RGB',(1180,660),'#eeeae2');dr=ImageDraw.Draw(sheet);dr.text((20,10),'No-Banner stages / canonical192x224 and modeled existing UI shapes, not live screenshots',fill='#222',font=font)
for n in (1,2):
 d=BASE/f'char-nobanner-{n:02}';y=45+(n-1)*300;dr.text((20,y),f'0{n}: '+('unstable collective' if n==1 else 'negotiated coordinator'),fill='#222',font=font)
 x=20
 for name,label,size in [('card-preview.png','192x224 card',(192,224)),('qa-ui-desktop-dialogue.png','92x112 desktop, 2x view',(184,224)),('qa-ui-mobile-dialogue.png','64x88 mobile, 2x view',(128,176)),('qa-ui-roster.png','44x44 roster, 3x view',(132,132)),('qa-ui-hud.png','96x112 HUD, 2x view',(192,224))]:
  p=Image.open(d/name);dr.text((x,y+25),label,fill='#222',font=small);sheet.paste(p.resize(size,Image.Resampling.NEAREST).convert('RGB'),(x,y+47));x+=size[0]+32
sheet.save(PAIR/'qa-card-and-modeled-crops.png')
proof=Image.new('RGB',(1536,820),'#ddd8d0');pd=ImageDraw.Draw(proof);pd.text((16,8),'Full whole-source black / white comparison (downscaled proof, native composites also saved)',fill='#222',font=font)
for n in (1,2):
 d=BASE/f'char-nobanner-{n:02}'
 for j,color in enumerate(('white','black')):
  p=Image.open(d/f'qa-native-{color}.png');proof.paste(p.resize((740,740),Image.Resampling.LANCZOS),((n-1)*768,40)) if j==0 else None
# Separate sheets for white and black keep all sources larger.
for color in ('white','black'):
 proof=Image.new('RGB',(1536,810),'#ddd8d0');pd=ImageDraw.Draw(proof);pd.text((16,8),f'Whole-source stage comparison on {color}; 01 unstable / 02 coordinator',fill='#222',font=font)
 for n in (1,2):proof.paste(Image.open(BASE/f'char-nobanner-{n:02}'/f'qa-native-{color}.png').resize((750,750),Image.Resampling.LANCZOS),((n-1)*768+9,42))
 proof.save(PAIR/f'qa-stage-comparison-{color}.png')
(PAIR/'pair-registration-proposal.json').write_text(json.dumps(summary,indent=2))
print(json.dumps([{'topic':x['topicId'],'placement':x['selectedPlacement']} for x in summary],indent=2))
