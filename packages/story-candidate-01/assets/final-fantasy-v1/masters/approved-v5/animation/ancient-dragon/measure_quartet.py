from PIL import Image,ImageDraw
from pathlib import Path
import json,numpy as np
B=Path(__file__).parent;s=64/940;canonical=(1024,962);orig=(6,6);fw,fh=128,88
rois={
'idle':{'near-hind':(810,850,860,930),'far-hind':(885,835,923,904),'near-fore':(930,850,1150,980),'far-fore':(1200,850,1400,960)},
'walk-a':{'near-hind':(700,820,870,940),'far-hind':(890,750,1060,880),'near-fore':(1010,650,1180,830),'far-fore':(1160,810,1400,960)},
'attack':{'near-hind':(680,820,810,930),'far-hind':(800,810,890,900),'near-fore':(880,830,1120,980),'far-fore':(1140,830,1390,960)},
'walk-b':{'near-hind':(700,750,825,910),'far-hind':(825,820,935,910),'near-fore':(930,850,1160,980),'far-fore':(1280,700,1510,860)}}
planted={'idle':['near-hind','far-hind','near-fore','far-fore'],'walk-a':['near-hind','far-fore'],'attack':['near-hind','far-hind','near-fore','far-fore'],'walk-b':['far-hind','near-fore']}
poses={}
for pose,footrois in rois.items():
 im=Image.open(B/pose/'master.png');a=np.array(im.getchannel('A'));feet={}
 for n,(l,t,r,b) in footrois.items():
  ys,xs=np.where(a[t:b,l:r]>127);sole=int(ys.max()+t);band=(ys+t)>=sole-3;feet[n]={'roi':[l,t,r,b],'bottomBandX':float((xs[band]+l).mean()),'soleY':sole,'planted':n in planted[pose],'caveat':'Visible far-hind toe partly occluded by near foreleg; bounded manual estimate' if n=='far-hind' and pose in ['idle','attack','walk-b'] else None}
 foot=[round(sum(v['bottomBandX'] for v in feet.values())/4),max(feet[k]['soleY'] for k in planted[pose])];off=[(canonical[i]-foot[i])*s for i in range(2)];bound=Image.fromarray((a>127).astype('uint8')*255).getbbox();poses[pose]={'nativeSize':im.size,'padRight':1586-im.width,'padBottom':992-im.height,'sourceSupportAnchorEstimated':foot,'feet':feet,'registrationOffsetLogical':off,'nativeVisibleBoundsAlphaGreaterThan127':bound,'logicalVisibleBounds':[orig[0]+off[0]+bound[0]*s,orig[1]+off[1]+bound[1]*s,orig[0]+off[0]+bound[2]*s,orig[1]+off[1]+bound[3]*s],'fullCanvasFits':all([orig[0]+off[0]>=0,orig[1]+off[1]>=0,orig[0]+off[0]+1586*s<=fw,orig[1]+off[1]+992*s<=fh])}
print(json.dumps(poses,indent=2));reg={'sharedScale':s,'scaleFraction':'64/940','bodyReferenceSourcePixels':940,'bodyTargetHeightLogicalPixels':64,'canonicalSourceFoot':canonical,'commonSourceCanvas':[1586,992],'paddingPolicy':'Walk-a1585x992 padded one transparent right column for common full canvas; all raw/master files immutable. Other poses1586x992.','proposedCommonFrameSize':[fw,fh],'proposedOrigin':orig,'proposedRuntimePivot':[orig[0]+canonical[0]*s,orig[1]+canonical[1]*s],'registrationMethod':'Read alpha>127 in bounded visible paw ROIs; mean of four bottom-four-row foot-band x estimates, including lifted paw stance projection, rounded once. Y is lowest planted paw. Whole-canvas translation only using idle-derived64/940. No independent pose fit, crop, recolor or alpha edits.','artFootprint':[2,2],'gameplayRulesChanged':False,'poses':poses};(B/'registration-evidence.json').write_text(json.dumps(reg,indent=2))
for density in [1,2]:
 out=Image.new('RGBA',(fw*4*density,(fh+22)*density),'#e9e6dc');dr=ImageDraw.Draw(out)
 for i,pose in enumerate(rois):
  im=Image.open(B/pose/'master.png').convert('RGBA');off=poses[pose]['registrationOffsetLogical'];size=(round(im.width*s*density),round(im.height*s*density));im=im.resize(size,Image.Resampling.LANCZOS);out.alpha_composite(im,(round((i*fw+orig[0]+off[0])*density),round((orig[1]+off[1])*density)));dr.text(((i*fw+5)*density,fh*density),pose,fill='black')
 out.convert('RGB').save(B/f'quartet-native-scale-{density}x.png')
