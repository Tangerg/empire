"""Frozen manually inspected idle0 front-boot ROI; not whole-alpha feet, not walk simulation.
Current base is derived from runtime cap geometry plus the authored stone height.
"""
from PIL import Image
import json
from pathlib import Path
root=Path('/workspace/scratch/84c9fe948755');g=root/'empire-c01/godot';out=root/'c07-static-preview'
asset=next(a for a in json.load(open(g/'data/selected-assets.json'))['assets'] if a.get('id')=='torren-stage01')
level=next(l for l in json.load(open(g/'data/grayflag-campaign-levels.json')) if l['id']=='c01-07')
im=Image.open(g/asset['resourcePath'].removeprefix('res://'));anchor=asset['frameAnchors'][0];scale=asset['presentationScale'];sole=[]
for x in range(315,441):
 ys=[y for y in range(490,565) if im.getpixel((x,y))[3]>=200]
 if ys:sole.append((x,max(ys)))
assert len(sole)==93
# Actual formal cap hull shifted by design height, not an independent advancing footprint.
def hull(points):
 points=sorted(set(tuple(p) for p in points))
 def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
 low=[];up=[]
 for p in points:
  while len(low)>=2 and cross(low[-2],low[-1],p)<=0:low.pop()
  low.append(p)
 for p in reversed(points):
  while len(up)>=2 and cross(up[-2],up[-1],p)<=0:up.pop()
  up.append(p)
 return low[:-1]+up[:-1]
layout=json.load(open(g/'data/scenes/c01-07-selected-assembly-ground.json'));catalog=json.load(open(g/'data/scenes/c01-07-selected-assembly-objects.json'))['objects']
def base(part,height):
 i=next(i for i in layout['objects'] if i['id']=='granary-courtyard-wall-'+part);d=json.load(open(g/catalog[i['assetId']]['surfaceGeometryPath'].removeprefix('res://')))
 return hull([[p[0]+i['contact'][0]*16,p[1]+i['contact'][1]*16+height] for m in d['members'] if m['id'].startswith('continuous-cap-') for p in m['polygonPixels']])
def inside(q,poly):
 x,y=q;result=False
 for i,(a,b) in enumerate(poly):
  e,f=poly[(i+1)%len(poly)]
  if (x-a)*(f-b)-(y-b)*(e-a)==0 and min(a,e)<=x<=max(a,e) and min(b,f)<=y<=max(b,f):return True
  if (b>y)!=(f>y) and x<(e-a)*(y-b)/(f-b)+a:result=not result
 return result
old=[(369,251),(379,246),(387,252),(377,257)];new=base('terminal-pier',25);main=base('main',26)
def mapped(cell,facing):
 sign=-1 if facing=='west' else 1
 return [(16*cell[0]+8+(x-anchor[0])*scale*sign,16*cell[1]+8+(y-anchor[1])*scale) for x,y in sole]
rows=[]
for cell in [(24,15),(24,16)]:
 for facing in ['east','west']:
  counts=[sum(inside(p,poly) for p in mapped(cell,facing)) for poly in [old,new,main]];rows.append({'cell':cell,'facing':facing,'oldNewMainCounts':counts})
assert rows[1]['oldNewMainCounts']==[28,0,0]
legal=[(x,y) for y in range(14,18) for x in range(18,27) if level['terrain'][y][x]=='p'];assert len(legal)==20
for cell in legal:
 for facing in ['east','west']:assert not any(inside(p,new) or inside(p,main) for p in mapped(cell,facing)),(cell,facing)
sweep=[]
for facing in ['east','west']:
 for step in range(17):
  pts=mapped((24,15+step/16),facing);n=sum(inside(p,new) for p in pts)
  if n:sweep.append({'facing':facing,'stepOf16':step,'inside':n})
result={'scope':'One real idle0 front boot,93fixedA>=200lower-edge samples; current footprint derived from formal cap+authored height. Not all appearances/animations or gameplay.','sourceAsset':asset['resourcePath'],'frameRegion':asset['frameRegions'][0],'anchor':anchor,'scale':scale,'roi':[315,490,440,564],'soleFramePixels':sole,'oldRejectedBase':old,'currentFormalCapDerivedBase':new,'mainFormalCapDerivedBase':main,'standingControls':rows,'legal20':legal,'standingBothFacingsPass':True,'idleOnlyConservativeContinuousSweep':sweep,'walkPathAcceptance':'pending; idle sweep cannot establish actual walk frame collision'}
(out/'granary-courtyard-boot-result.json').write_text(json.dumps(result,indent=2)+'\n');print('GRANARY_FROZEN_IDLE_BOOT_STANDING_COMPLETE',rows,'sweepDiagnostics',sweep)
