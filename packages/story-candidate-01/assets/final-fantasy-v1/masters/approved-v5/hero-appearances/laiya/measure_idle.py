from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
b=Path(__file__).parent;d=b/'idle';im=Image.open(d/'master.png').convert('RGBA');a=np.array(im)[:,:,3]
q=json.loads((d/'alpha-qa.json').read_text());rois={'image-left-boot':[630,840,850,990],'image-right-boot':[930,840,1140,990]};feet={}
for name,(x0,y0,x1,y1) in rois.items():
 yy,xx=np.where(a[y0:y1,x0:x1]>127);yy+=y0;xx+=x0;sole=int(yy.max());band=xx[yy>sole-4];feet[name]={'roi':[x0,y0,x1,y1],'bounds':[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)],'soleY':sole,'bottomFourRowsMeanX':float(band.mean())}
x0,y0,x1,y1=730,150,1100,480;yy,xx=np.where(a[y0:y1,x0:x1]>127);crown=int(yy.min()+y0);meanSole=sum(f['soleY'] for f in feet.values())/2;body=meanSole-crown;scale=44/body;foot=[round(sum(f['bottomFourRowsMeanX'] for f in feet.values())/2),max(f['soleY'] for f in feet.values())];finial=14
probes=[]
for name,rect in {'between-boots':[878,860,885,867],'flag-to-arm-gap':[552,535,559,542],'scabbard-to-pole-gap':[673,741,680,748]}.items():
 x0,y0,x1,y1=rect;v=a[y0:y1,x0:x1];probes.append({'name':name,'rectangle':rect,'maxAlpha':int(v.max()),'zeroPixels':int((v==0).sum())});assert v.max()==0
measurement={'hairROI':[730,150,1100,480],'hairCrownY':crown,'bootMeasurements':feet,'meanBootSoleY':meanSole,'bodyHeightSourcePixels':body,'bodyTargetLogicalPixels':44,'proposedSharedScale':scale,'scaleFraction':'44/784','proposedSourceFoot':foot,'sourceFootMeanXBeforeRounding':sum(f['bottomFourRowsMeanX'] for f in feet.values())/2,'finialTopY':finial,'flagTopToLowestBootSourcePixels':foot[1]-finial,'flagTopToLowestBootLogicalPixels':(foot[1]-finial)*scale,'bodyScaleExcludesFlag':True,'nativeCanvas':[1586,992],'candidateFrame':[104,64],'candidateOrigin':[8,4],'logicalFullCanvasBounds':[8,4,8+1586*scale,4+992*scale]}
q.update({'bodyMeasurement':measurement,'suggestedSourceFoot':foot,'negativeSpaceProbes':probes,'semanticReview':'Independent named18-year-old female Laiya: brown side braid, exposed face, original right-hand plain torn gray banner at image-left, original left steel wrist guard at image-right, gray-blue quilted/mail-edged armor, brown belt/trousers/boots. Added one complete modest sheathed short straight sword per coordinator approval; original card has no visible sword. No generic banner-guard moustache, helmet, back shield or white flag emblem. Whole finial/flag/pole/sheath/boots visible. Native and white/black composites inspected.','status':'Idle candidate only, awaiting coordinator native identity/body-scale and appearance-interface approval; no other poses generated'})
(d/'alpha-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n');(b/'idle-review.json').write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
req=json.loads((d/'request.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();assert sha(d/'master.png')==sha(d/'raw.png')==sha(q['nativeSource'])==q['sha256'];assert sha(d/'prompt.txt')==req['promptSha256']
for p,h in req['referencedImageSha256'].items():assert sha(p)==h
source=json.loads((b/'source-evidence.json').read_text());source['status']=q['status'];source['generationAuthorization']='Coordinator2026-10-19 20:46UTC explicitly approved one independent transparent idle, plain flag and original hand assignment; modest complete short-sword scabbard addition; wait on remaining poses.';source['idleNativeSha256']=q['sha256'];source['idleReview']='idle-review.json';source['pending']='Coordinator idle/native-scale review and appearance-interface contract before other3poses.';(b/'source-evidence.json').write_text(json.dumps(source,ensure_ascii=False,indent=2)+'\n')
# Uniform native-scale diagnostic. Each source uses its single accepted/proposed body scale, full canvas, common baseline; no alpha edits.
sword=Image.open('/workspace/shared/empire-art-rebuild/approved-v5/swordsman/master.png').convert('RGBA');entries=[('Laiya pilot44',im,scale,foot),('Approved swordsman44',sword,1/18,[800,887])];review=Image.new('RGB',(512,360),(220,220,220));draw=ImageDraw.Draw(review)
for row,bgcol in enumerate(['white','black']):
 for col,(label,img,s,anchor) in enumerate(entries):
  tx=64-anchor[0]*s;ty=66-anchor[1]*s;full=[tx,ty,tx+img.width*s,ty+img.height*s];assert full[0]>=0 and full[1]>=0 and full[2]<=128 and full[3]<=80
  preview=img.transform((128,80),Image.Transform.AFFINE,(1/s,0,-tx/s,0,1/s,-ty/s),resample=Image.Resampling.BICUBIC,fillcolor=(0,0,0,0));bg=Image.new('RGBA',(128,80),bgcol);bg.alpha_composite(preview);review.paste(bg.convert('RGB').resize((256,160),Image.Resampling.NEAREST),(col*256,row*180+20));draw.text((col*256+5,row*180+5),label+' / '+bgcol,fill=(0,0,0))
review.save(d/'native-scale-comparison.png')
print(json.dumps({'bodyMeasurement':measurement,'integrityChecks':'PASS','comparison':str(d/'native-scale-comparison.png')},indent=2))
