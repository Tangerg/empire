"""Bounded new low stone connection; original hidden joins are not claimed reconstructed."""
from pathlib import Path
import json
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview')
front=[[680,480],[685,484],[699,493],[706,493],[714,491]]
rear=[[685,476],[699,480],[714,486]]
poly=rear+list(reversed(front))
texture='res://assets/native/c07-layered-v1/central-landmark-core-v1-candidate.png'
topuv=[[410+(x-680)/34*80,650+(y-476)/17*200] for x,y in poly]
cap={'textureResourcePath':texture,'supportMode':'horizontal-plane','supportLine':front,'members':[{'id':'low-bearing-cap','polygon':[[x,y-2] for x,y in poly],'uv':topuv,'tint':'ffffff'}],'scope':'NEW low continuous stone connector, 2px high. Ground front is separately authored; cap is horizontal carrying surface. Original706490 residual explicitly included, no rule changes.'}
faces=[]
for j,(a,b) in enumerate(zip(front,front[1:])):
 uv=[[300,1245],[455,1320],[455,1335],[300,1260]] if b[1]>=a[1] else [[550,1310],[780,1195],[780,1212],[550,1327]]
 faces.append({'id':f'low-bearing-riser-{j}','polygon':[[a[0],a[1]-2],[b[0],b[1]-2],b,a],'uv':uv,'tint':'ffffff'})
riser={'textureResourcePath':texture,'supportLine':front,'members':faces,'scope':'NEW 2px low stone connector front, physical foot separate from cap and planted objects.'}
for n,d in [('cap',cap),('riser',riser)]: (p/f'central-west-bearing-{n}-source.json').write_text(json.dumps(d,indent=2)+'\n')
(p/'central-west-bearing-design.json').write_text(json.dumps({'newDesign':True,'groundFront':front,'groundRear':rear,'height':2,'fixedResidual':[706,490],'platformConnection':[714,491],'scope':'Proposed low continuous structural apron, not a whole blocked-cell fill. Existing wooden planter remains separate owner; rear white and low pier also separate.'},indent=2)+'\n')
