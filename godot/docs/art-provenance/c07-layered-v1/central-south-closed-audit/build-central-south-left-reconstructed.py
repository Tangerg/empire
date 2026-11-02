"""One coherent NEW left container design, not patch slabs or original hidden-foot recovery."""
from pathlib import Path
import numpy as np,json,math
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview'
foot=np.array([[733.,509.],[749.,518.],[758.,514.],[743.,507.]])
top=foot-[0,6];center=foot.mean(axis=0);woodFoot=center+(foot-center)*.85-[0,6];woodTop=woodFoot-[0,7]
parts=[]
def add(n,world,uv,support='base',coverage='opaque'):
 parts.append({'id':n,'polygon':np.asarray(world).tolist(),'uv':uv,'support':support,'coverage':coverage,'tint':'ffffff'})
add('stone-top',top,[[700,950],[780,920],[800,930],[720,960]])
add('stone-left',[top[0],top[1],foot[1],foot[0]],[[300,790],[580,930],[580,1000],[300,860]])
add('stone-right',[top[1],top[2],foot[2],foot[1]],[[700,950],[1000,820],[1000,870],[700,1000]])
add('wood-left',[woodTop[0],woodTop[1],woodFoot[1],woodFoot[0]],[[315,545],[625,690],[625,880],[315,735]])
add('wood-right',[woodTop[1],woodTop[2],woodFoot[2],woodFoot[1]],[[640,690],[1010,525],[1010,755],[640,900]])
# Distinct wooden rim and soil with preserved upper-root coverage.
wc=woodTop.mean(axis=0);soil=wc+(woodTop-wc)*.82
add('soil',soil,[[610,620],[620,620],[620,630],[610,630]])
for i in range(4):
 j=(i+1)%4;add('rim-'+str(i),[woodTop[i],woodTop[j],soil[j],soil[i]],[[380,550],[580,645],[590,635],[390,540]])
uv=np.array([[300,230],[1000,230],[1000,470],[930,500],[900,545],[800,580],[760,630],[650,650],[550,610],[450,560],[350,530],[300,480]],float)
root=np.array([745.,501.]);world=(uv-[650,620])*.035+root
add('rooted-foliage',world,uv.tolist(),'root','native-alpha')
for part in parts:
 xs=[q[0] for q in part['polygon']];lo=math.floor(min(xs));hi=math.ceil(max(xs))
 if part['support']=='root':line=[[lo,514],[hi,514]]
 else:
  def f(x):return 509+(x-733)*9/16 if x<=749 else 518-(x-749)*4/9
  line=[[lo,f(lo)]]+([[749,518]] if lo<749<hi else [])+[[hi,f(hi)]]
 if part['id'].startswith('rim-'):
  ri=int(part['id'].split('-')[-1]);a=woodTop[ri]+[0,13];z=woodTop[(ri+1)%4]+[0,13]
  if a[0]>z[0]:a,z=z,a
  def rimY(x):return float(a[1]+(z[1]-a[1])*(x-a[0])/(z[0]-a[0]))
  line=[[lo,rimY(lo)],[hi,rimY(hi)]]
 source={'textureResourcePath':'res://assets/native/c07-layered-v1/central-front-wood-planter-v1-candidate.png','supportLine':line,'members':[part]}
 if part['id']=='soil':source['supportMode']='horizontal-plane'
 if part['id'].startswith('rim-'):
  source['columnCuts']=[lo]+[x for x in range(lo+1,hi) if all(abs(x-v) >= .25 for v in xs)]+[hi]
 (p/('south-left-'+part['id']+'-source.json')).write_text(json.dumps(source,indent=2)+'\n')
(p/'central-south-left-reconstructed-source.json').write_text(json.dumps({'scope':__doc__,'footprint':foot.tolist(),'stoneHeight':6,'woodHeight':7,'rootOnSoil':root.tolist(),'rootGround':[745,514],'parts':parts},indent=2)+'\n')
print('components',len(parts),'whole checked foundation; right/back NEW closure')
