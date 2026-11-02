"""Conservative column-foot prism bounds plus real static walk support contours.
No engine commands or inferred pass for other actors/paths.
"""
from pathlib import Path
import json,runpy
r=Path('/workspace/scratch/84c9fe948755');out=r/'c07-static-preview';w=runpy.run_path(str(out/'verify-granary-courtyard-walk.py'));g=r/'empire-c01/godot/data/scenes'
layout=json.load(open(g/'c01-07-selected-assembly-ground.json'));i=next(i for i in layout['objects'] if i['id']=='granary-courtyard-timber-left');d=json.load(open(g/'c01-07-granary-courtyard-timber-left-surface.json'));origin=[v*16 for v in i['contact']];hull=w['b']['hull'];caps=[]
# Heights are source construction metadata. Current world cap geometry comes only from formal surface.
for n,height in enumerate([26.5,26,28.5]):
 points=[[p[0]+origin[0],p[1]+origin[1]+height] for m in d['members'] if m['id'].startswith(f'post-{n}-cap-') for p in m['polygonPixels']]
 caps.append(hull(points))
level=w['b']['level'];legal=[(x,y) for y in range(13,19) for x in range(15,28) if level['terrain'][y][x]=='p'];edges=[(a,b) for a in legal for b in legal if abs(a[0]-b[0])+abs(a[1]-b[1])==1];bad=[];count=0
for a,b in edges:
 dx=b[0]-a[0]
 for west in ([False,True] if dx==0 else [dx<0]):
  for f in w['frames']:
   for p,q in zip(w['mapped'](a,f,west),w['mapped'](b,f,west)):
    count+=1
    for n,cap in enumerate(caps):
     if w['hits'](p,q,cap):bad.append({'from':a,'to':b,'west':west,'frame':f['index'],'post':n,'soleFrom':p,'soleTo':q})
report={'scope':'Conservative cap extrusions contain post feet; real Torren89/86 front boot contours; static math only, no all-role or all-map claim','capFootConservativeBounds':caps,'legalCells':legal,'directedEdges':len(edges),'soleSegments':count,'violations':bad,'pass':not bad}
(out/'granary-courtyard-timber-feet-result.json').write_text(json.dumps(report,indent=2)+'\n');print('TIMBER_FOOT_BOUNDS',len(edges),count,len(bad));assert not bad,bad[:2]
