"""Bounded rear-west wooden seat and planted enclosure; hidden foundation is NEW design."""
from pathlib import Path
import numpy as np,json,math
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview')
texture='res://assets/native/c07-layered-v1/central-front-wood-planter-v1-candidate.png'
parts=[]
def add(n,world,uv,line,coverage='opaque'):
 a=np.asarray(world,float);xs=a[:,0];lo=math.floor(min(xs));hi=math.ceil(max(xs));v=np.asarray(line,float);v=v[np.argsort(v[:,0])]
 def at(x):return float(v[0,1]+(v[-1,1]-v[0,1])*(x-v[0,0])/(v[-1,0]-v[0,0]))
 src={'textureResourcePath':texture,'supportLine':[[lo,at(lo)],[hi,at(hi)]],'members':[{'id':n,'polygon':a.tolist(),'uv':uv,'tint':'ffffff','coverage':coverage}]}
 # Exact authored polygon remains whole; avoid nearly coincident clip vertices.
 src['columnCuts']=[lo]+[x for x in range(lo+1,hi) if all(abs(x-v) >= .25 for v in xs)]+[hi]
 (p/f'rear-west-{n}-source.json').write_text(json.dumps(src,indent=2)+'\n');parts.append({'id':n,'polygon':a.tolist(),'source':src})
front=[[722,460],[740,452]];back=[[717,455],[735,447]]
woodLight=[[660,705],[970,567],[970,640],[660,778]]
woodDark=[[370,600],[550,685],[550,745],[370,660]]
# Front side is visibly solid original wood, not an invented transparent slit.
add('seat-apron',[[722,452],[740,444],[740,450.5],[722,458.5]],woodLight,front)
add('seat-top',[[717,447],[735,439],[740,444],[722,452]],[[655,688],[970,548],[995,555],[680,700]],front)
add('backrest',[[717,440],[735,432],[735,439],[717,447]],woodLight,back)
add('backrest-cap',[[716.7,439.5],[734.7,431.5],[735.5,432],[717.5,440]],[[680,700],[975,565],[980,570],[685,705]],back)
# Two visible near wooden ends agree with the original witnesses; rear support is design completion.
for name,a in [('left',[722,460]),('right',[739.5,452])]:
 x,y=a;add(name+'-leg',[[x-1,y-9],[x+1,y-9.6],[x+1,y-.6],[x-1,y]],[[630,710],[650,700],[650,880],[630,890]],[[x-1,y],[x+1,y-.6]])
add('left-side',[[717,447],[722,452],[722,459],[717,454]],woodDark,[[717,455],[722,460]])
foot=np.array([[735.,449.],[744.,454.],[750.,450.],[741.,445.]])
top=foot-[0,6]
add('bed-top',top,[[700,950],[780,920],[800,930],[720,960]],foot[:3:2])
add('bed-left',[top[0],top[1],foot[1],foot[0]],[[300,790],[580,930],[580,1000],[300,860]],foot[:2])
add('bed-right',[top[1],top[2],foot[2],foot[1]],[[700,950],[1000,820],[1000,870],[700,1000]],foot[1:3])
uv=np.array([[300,230],[1000,230],[1000,470],[930,500],[900,545],[800,580],[760,630],[650,650],[550,610],[450,560],[350,530],[300,480]],float)
root=np.array([743.,443.]);world=(uv-[650,620])*.034+root
add('bed-foliage',world,uv.tolist(),[[math.floor(world[:,0].min()),449],[math.ceil(world[:,0].max()),449]],'native-alpha')
(p/'central-rear-west-group-source.json').write_text(json.dumps({'scope':__doc__,'woodFrontWitnesses':front,'woodBackDesign':back,'bedHiddenFootDesign':foot.tolist(),'foliageRoot':root.tolist(),'foliageGroundRoot':[743,449],'parts':parts},indent=2)+'\n')
print('rearwest parts',len(parts))
