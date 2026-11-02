"""One bounded NEW DESIGN, formal native-alpha surfaces, frozen generated material.
Physical base follows independently checked blocked-ground polygon. Foliage roots
are embedded in its soil at height14; no shader threshold or per-object wrapper.
"""
from pathlib import Path
import json, math, shutil
import numpy as np
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';base=r/'empire-c01/godot/data/scenes'
# Keep study texture external until actual visual/contract review succeeds.
shutil.copy2(r/'generated_images/exec-35b3ff1d-189f-414b-ac74-6ebbd2434380.png',p/'central-rear-east-bed-material.png')
O=np.array([770.,407.]);foot=np.array([[782.,448.],[790.,454.],[819.,469.],[811.,463.]])
top=foot-[0,14];A,B,C,D=top
lerp=lambda a,b,t:a+(b-a)*t
# The split leaves one physically connected planted compartment and one seat.
t=.64;E=lerp(B,C,t);F=lerp(A,D,t)
parts=[]
def add(name,poly,uv,support,coverage='opaque'):
 parts.append(dict(id=name,polygon=np.asarray(poly).tolist(),uv=uv,support=support,coverage=coverage))
# Stone faces use the original generated stone facets, NOT full-image height scaling.
add('front-stone',[B,C,foot[2],foot[1]],[[415,560],[580,650],[580,770],[415,680]],'front')
add('left-stone',[A,B,foot[1],foot[0]],[[340,505],[380,525],[380,650],[340,630]],'left')
# Top rim / soil and seat are distinct physical surfaces, all on the base.
soil=[lerp(A,B,.17),lerp(A,B,.83),lerp(F,E,.83),lerp(F,E,.17)]
add('soil',soil,[[610,605],[625,620],[670,635],[665,618]],'front')
for name,poly in [('rear-rim',[A,F,soil[3],soil[0]]),('front-rim',[soil[1],soil[2],E,B]),('left-rim',[A,soil[0],soil[1],B])]:
 add(name,poly,[[360,480],[400,492],[400,500],[360,490]],'front')
add('wood-seat',[F,E,C,D],[[890,590],[750,675],[985,795],[1120,722]],'front')
# The hand-reviewed lower contour follows leaves/roots above the original stone rim.
# This keeps the branching root ends; they enter new soil, not an alpha-erased cutout.
leafuv=np.array([[250,40],[1000,40],[1000,520],[943,526],[904,563],[860,579],[800,616],[738,619],[691,643],[635,615],[595,592],[534,559],[469,536],[409,500],[383,455],[250,455]],float)
root=np.array([793.,441.]);leafworld=(leafuv-[650,620])*.055+root
add('rooted-foliage',leafworld,leafuv.tolist(),'root','native-alpha')
# Export each physical component through the existing Godot triangulation exporter.
for part in parts:
 poly=np.asarray(part['polygon']);lo=math.floor(poly[:,0].min());hi=math.ceil(poly[:,0].max())
 if part['support']=='root':line=[[lo,455],[hi,455]]
 elif part['support']=='left':line=[[lo,448+(lo-782)*6/8],[hi,448+(hi-782)*6/8]]
 else:line=[[lo,454+(lo-790)*15/29],[hi,454+(hi-790)*15/29]]
 source={'textureResourcePath':'res://central-rear-east-bed-material.png','supportLine':line,'members':[dict(id=part['id'],polygon=part['polygon'],uv=part['uv'],tint='ffffff',coverage=part['coverage'])]}
 (p/('rear-bed-'+part['id']+'-source.json')).write_text(json.dumps(source,indent=2)+'\n')
(p/'central-rear-east-bed-native-source.json').write_text(json.dumps({'scope':__doc__,'origin':O.tolist(),'footprint':foot.tolist(),'rootOnSoil':root.tolist(),'rootGround':[793,455],'parts':parts},indent=2)+'\n')
s=json.load(open(base/'c01-07-selected-assembly.json'));g=json.load(open(base/'c01-07-selected-assembly-ground.json'));c=json.load(open(base/'c01-07-selected-assembly-objects.json'))
c['objects']['central-rear-east-bed']={'resourcePath':'res://central-rear-east-bed-material.png','pixelSize':[1536,1024],'anchorPixels':[0,0],'surfaceGeometryPath':'res://central-rear-east-bed-native-surface.json','contactShadowRadiiPixels':None}
g['objects'].append({'id':'central-rear-east-bed','assetId':'central-rear-east-bed','contact':(O/16).tolist(),'scale':1.,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
g['objectCatalogPath']='res://central-rear-east-bed-native-objects.json';s['assembledGroundPath']='res://central-rear-east-bed-native-ground.json'
for suffix,v in [('',s),('-ground',g),('-objects',c)]: (p/f'central-rear-east-bed-native{suffix}.json').write_text(json.dumps(v,indent=2)+'\n')
print('Prepared',len(parts),'physical components')
