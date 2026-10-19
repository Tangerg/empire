from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib,math,shutil
r=Path(__file__).parent;scale=54/909.5;origin=[14,6];canonical=[822,975];frame=[128,80];common=[1586,993];out=r/'export-preview';out.mkdir(exist_ok=True)
rois={
'idle':{'near-hind':[410,865,540,970],'far-hind':[650,870,785,960],'near-fore':[845,875,980,980],'far-fore':[1080,880,1220,970]},
'walk-a':{'near-hind':[370,860,490,970],'far-hind':[680,815,790,915],'near-fore':[860,780,950,880],'far-fore':[1130,880,1280,980]},
'attack':{'near-hind':[240,860,355,960],'far-hind':[625,860,755,950],'near-fore':[970,875,1105,975],'far-fore':[1180,860,1310,960]},
'walk-b':{'near-hind':[615,815,735,910],'far-hind':[400,860,520,965],'near-fore':[1000,880,1130,980],'far-fore':[1040,770,1140,865]}}
contact={'idle':['near-hind','far-hind','near-fore','far-fore'],'walk-a':['near-hind','far-fore'],'attack':['near-hind','far-hind','near-fore','far-fore'],'walk-b':['far-hind','near-fore']}
reviews={
'idle':'Four planted hooves; upright rider, short spear up-left in near arm and reins in far arm. Source identity preserved.',
'walk-a':'Near hind and far fore support. Near fore is folded/lifted; shadowed far hind is lifted. Four independent connected legs. Rider/spear/reins/antlers and torso scale stable.',
'attack':'Selected attempt04. Near shoulder foreground arm clearly connects to low one-hand spear thrust; far arm holds reins as idle. Braced four-hoof stance. Spear shaft corrected without source editing; short spear remains complete and no FX added. Earlier two hand-swapped attacks and longer-shaft attempt03 retained.',
'walk-b':'Far hind and near fore support. Near hind is swung forward and lifted; far fore is folded and lifted. This is opposite actual diagonal hoof contact from walk-a; four connected legs, stable rider and equipment.'}
probes={
'idle':[('hind-gap',584,880),('middle-gap',805,884),('fore-gap',1015,898),('antler-gap',1150,213)],
'walk-a':[('hind-gap',580,880),('middle-gap',805,885),('fore-gap',1035,882),('antler-gap',1150,213)],
'attack':[('hind-gap',490,880),('middle-gap',820,875),('fore-gap',1120,920),('antler-gap',1150,190)],
'walk-b':[('hind-gap',570,880),('middle-gap',815,870),('fore-gap',1140,875),('antler-gap',1150,213)]}
entries={};images={};frames=[]
for pose in rois:
 p=r/pose;im=Image.open(p/'master.png');a=np.array(im.getchannel('A'));feet=[]
 for name,roi in rois[pose].items():
  x0,y0,x1,y1=roi;yy,xx=np.where(a[y0:y1,x0:x1]>127);sole=int(yy.max()+y0);xs=np.where(a[sole-3:sole+1,x0:x1]>127)[1]+x0
  feet.append({'hoof':name,'roi':roi,'soleY':sole,'bottomFourRowsMeanX':float(xs.mean()),'state':'planted' if name in contact[pose] else 'lifted'})
 anchor=[round(np.mean([f['bottomFourRowsMeanX'] for f in feet])),max(f['soleY'] for f in feet)];offset=[(canonical[i]-anchor[i])*scale for i in range(2)];pos=[origin[i]+offset[i] for i in range(2)];bounds=[pos[0],pos[1],pos[0]+common[0]*scale,pos[1]+common[1]*scale];fit=bounds[0]>=0 and bounds[1]>=0 and bounds[2]<=128 and bounds[3]<=80
 q=json.loads((p/'alpha-qa.json').read_text());q.update({'feetMeasurement':feet,'sourceFoot':anchor,'sharedScale':scale,'sharedScaleFraction':'54/909.5','registrationOffsetLogical':offset,'commonSourceCanvas':common,'padRight':common[0]-im.width,'padBottom':common[1]-im.height,'fullCanvasBoundsLogical':bounds,'wholeCanvasFits':fit,'semanticReview':reviews[pose],'negativeSpaceProbes':[]})
 for name,cx,cy in probes[pose]:
  candidates=[]
  for y in range(cy-22,cy+23):
   for x in range(cx-22,cx+23):
    block=a[y-3:y+4,x-3:x+4]
    if block.shape==(7,7) and not block.any():candidates.append(((x-cx)**2+(y-cy)**2,x,y))
  assert candidates,(pose,name)
  _,x,y=min(candidates);q['negativeSpaceProbes'].append({'name':name,'rect':[x-3,y-3,x+4,y+4],'maxAlpha':0,'zeroPixels':49})
 assert q['padRight']>=0 and q['padBottom']>=0
 assert fit and im.mode=='RGBA' and q['perimeterPixelsAlphaGreaterThan1']==0
 (p/'alpha-qa.json').write_text(json.dumps(q,indent=2))
 req=json.loads((p/'request.json').read_text());assert req['prompt']==(p/'prompt.txt').read_text();assert hashlib.sha256((p/'prompt.txt').read_bytes()).hexdigest()==req['promptSha256'];assert hashlib.sha256((p/'raw.png').read_bytes()).hexdigest()==q['sha256']==hashlib.sha256((p/'master.png').read_bytes()).hexdigest()
 for f,h in req['referenceSha256'].items():assert hashlib.sha256(Path(f).read_bytes()).hexdigest()==h
 entries[pose]={'master':str(p/'master.png'),'sha256':q['sha256'],'nativeSize':list(im.size),'sourceFoot':anchor,'registrationOffsetLogical':offset,'padRight':q['padRight'],'padBottom':q['padBottom'],'fullCanvasBoundsLogical':bounds,'wholeCanvasFits':fit,'feet':feet,'alphaQA':str(p/'alpha-qa.json'),'prompt':str(p/'prompt.txt'),'request':str(p/'request.json'),'semanticReview':reviews[pose]}
 for density in [1,2]:
  padded=Image.new('RGBA',tuple(common));padded.paste(im,(0,0));size=(round(common[0]*scale*density),round(common[1]*scale*density));res=padded.resize(size,Image.Resampling.LANCZOS);dst=Image.new('RGBA',(128*density,80*density));dst.paste(res,(round(pos[0]*density),round(pos[1]*density)));da=np.asarray(dst.getchannel('A'));edge=np.concatenate([da[0],da[-1],da[:,0],da[:,-1]]);assert edge.max()==0;dst.save(out/f'{pose}@{density}x.png');entries[pose][f'export{density}x']={'size':list(dst.size),'alphaBounds':dst.getchannel('A').getbbox(),'perimeterMaxAlpha':int(edge.max())}
  if density==1:images[pose]=dst
reg={'asset':'C01-UNIT-WHITE-STAG-RIDER','frameOrder':list(rois),'sharedScale':scale,'sharedScaleFraction':'54/909.5','bodyMeasurement':'Idle hair crown y58 to mean near-side hooves y967.5 =909.5sourcepixels, target54logical.','measurementHistory':'Initial manual hair top y60 produced54/907.5. ROI measurement corrected hair top to58 before other poses. Coordinator accepted54/909.5 at23:09UTC. No per-pose scale fit.','canonicalSourceFoot':canonical,'commonSourceCanvas':common,'sourcePaddingPolicy':'Common1586x993: idle1585x992 receives1right/1bottom; selected attack1586x992 receives0right/1bottom; walks1584x993 receive2right/0bottom. Only explicit transparent padding in derived export; raw/master bytes untouched.','frameSize':frame,'origin':origin,'registrationMethod':'Full canvas translated only at one shared scale. Contact x averages all four hoof bottom-band centers, y lowest sole; lifted vs planted semantics separately recorded.','poses':entries}
(r/'registration-evidence.json').write_text(json.dumps(reg,indent=2))
for name,color in [('white',(255,255,255,255)),('black',(22,22,22,255)),('olive',(94,106,73,255))]:
 panel=Image.new('RGBA',(512,100),color);dr=ImageDraw.Draw(panel)
 for i,pose in enumerate(rois):panel.alpha_composite(images[pose],(i*128,0));dr.text((i*128+35,84),pose,fill=(230,230,230) if name=='black' else (20,20,20))
 panel.save(out/f'four-{name}-native.png');panel.resize((1536,300),Image.Resampling.NEAREST).save(out/f'four-{name}-3x.png')
walk=[]
for pose in ['walk-a','walk-b']:
 bg=Image.new('RGBA',(128,80),(94,106,73,255));bg.alpha_composite(images[pose]);walk.append(bg.convert('RGB').resize((512,320),Image.Resampling.NEAREST))
walk[0].save(out/'walk-preview.gif',save_all=True,append_images=walk[1:],duration=160,loop=0)
print(json.dumps({p:{'foot':v['sourceFoot'],'offset':v['registrationOffsetLogical'],'feet':[(f['hoof'],f['soleY'],f['state'])for f in v['feet']],'bounds':v['fullCanvasBoundsLogical']}for p,v in entries.items()},indent=2))
