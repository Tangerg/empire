from pathlib import Path
import json,numpy as np
r=Path('/workspace/scratch/84c9fe948755');p=r/'c07-static-preview';b=r/'empire-c01/godot/data/scenes'
# Bounded NEW design: three independent ground bases; original right bench excluded.
base=np.array([[762.,521.],[767.,524.],[772.,521.],[767.,519.]])
base+=np.array([.75,0]);baseTop=base-[0,2];shaft=np.array([[763.5,519.],[767.,521.],[770.5,519.],[767.,517.7]])+np.array([.75,0])
shaftTop=shaft-[0,10];cap=shaftTop+np.array([[-1,-.5],[0,.5],[1,-.5],[0,-.5]]);capTop=cap-[0,1.5]
uTop=[[550,1000],[580,1015],[610,1000],[580,985]];uLeft=[[305,1085],[490,1180],[490,1240],[305,1145]];uRight=[[530,1180],[730,1080],[730,1140],[530,1240]]
m=[]
def prism(name,foot,top):
 m.append({'id':name+'-top','polygon':top.tolist(),'uv':uTop,'tint':'ffffff'})
 m.append({'id':name+'-left','polygon':[top[0].tolist(),top[1].tolist(),foot[1].tolist(),foot[0].tolist()],'uv':uLeft,'tint':'ffffff'})
 m.append({'id':name+'-right','polygon':[top[1].tolist(),top[2].tolist(),foot[2].tolist(),foot[1].tolist()],'uv':uRight,'tint':'ffffff'})
prism('plinth',base,baseTop);prism('shaft',shaft,shaftTop);prism('cap',cap,capTop)
src={'textureResourcePath':'res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png','supportLine':base[:3].tolist(),'members':m,'scope':'New independent southern low pier, actual near foot designed524 vs source522–523; no platform growth. Full body supported by its ground base, cap/shaft joined. Original golden finial not restored.'}
(p/'central-south-pier-source.json').write_text(json.dumps(src,indent=2)+'\n')
s=json.load(open(b/'c01-07-selected-assembly.json'));g=json.load(open(b/'c01-07-selected-assembly-ground.json'));c=json.load(open(b/'c01-07-selected-assembly-objects.json'))
for side in ['left','right']:
 d=json.load(open(p/f'central-south-{side}-planter-design.json'));g['objects'].append({'id':f'central-south-{side}-planter','assetId':'central-front-wood-planter-v1','contact':[v/16 for v in d['worldAnchor']],'scale':d['scale'],'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
pts=[q for v in m for q in v['polygon']];origin=[int(np.floor(min(q[k] for q in pts))) for k in [0,1]]
c['objects']['central-south-low-pier']={'resourcePath':src['textureResourcePath'],'pixelSize':[1024,1536],'anchorPixels':[0,0],'surfaceGeometryPath':'res://central-south-pier-surface.json','contactShadowRadiiPixels':None}
g['objects'].append({'id':'central-south-low-pier','assetId':'central-south-low-pier','contact':[v/16 for v in origin],'scale':1.,'selectionCell':None,'ownsMapStructureBody':False,'pointer':'pass-through'})
g['objectCatalogPath']='res://central-south-bearing-study-objects.json';s['assembledGroundPath']='res://central-south-bearing-study-ground.json'
for suffix,d in [('',s),('-ground',g),('-objects',c)]: (p/f'central-south-bearing-study{suffix}.json').write_text(json.dumps(d,indent=2)+'\n')
print('new low pier origin',origin,'three objects, original bench/finial absent, unselected')
