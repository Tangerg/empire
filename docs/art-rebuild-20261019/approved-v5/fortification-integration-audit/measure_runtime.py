from pathlib import Path
import json,re,collections
from PIL import Image
O=Path('/workspace/shared/empire-layout-redesign/fortification-review');p=json.loads((O/'runtime-scene-provenance.json').read_text());r=p['renders'][0]
ids={'stone-wall-straight':'straight','stone-wall-corner':'corner','stone-gatehouse':'gate'};bs={};props=[]
for id,n in ids.items():
 alpha=Image.open(O/f'actual-atlas-{n}@2x.png').getchannel('A');bs[id]=[v/2 for v in alpha.point(lambda a:255 if a>=128 else 0).getbbox()]
for s in r['namedPieces']:
 if s.get('cellId') not in ids:continue
 t=re.search(r'translate\(([-.\d]+) ([-.\d]+)\) scale\(([-.\d]+)(?: ([-.\d]+))?\)',s['transform']);tx,ty,sx=float(t[1]),float(t[2]),float(t[3]);sy=float(t[4]) if t[4] else sx
 x,y=round(s['origin'][0]/32-.5),round(s['origin'][1]/32-1.18);b=bs[s['cellId']];xs=[s['origin'][0]+tx+sx*b[i] for i in [0,2]];ys=[s['origin'][1]+ty+sy*b[i] for i in [1,3]]
 props.append({**s,'cell':[x,y],'effectiveScale':abs(sx),'alpha128WorldBounds':[min(xs),min(ys),max(xs),max(ys)]})
by={tuple(s['cell']):s for s in props};pairs=[];masks=collections.Counter()
for (x,y),s in by.items():
 n=[(x,y-1),(x+1,y),(x,y+1),(x-1,y)];mask=sum((1<<i) for i,c in enumerate(n) if c in by);masks[mask]+=1;s['adjacencyMaskNESW']=mask
 for dx,dy,axis in [(1,0,'east-west'),(0,1,'north-south')]:
  b=by.get((x+dx,y+dy))
  if b:
   aa=s['alpha128WorldBounds'];bb=b['alpha128WorldBounds'];gap=bb[0]-aa[2] if dx else bb[1]-aa[3]
   pairs.append({'first':[x,y],'second':[x+dx,y+dy],'axis':axis,'firstArt':s['cellId'],'secondArt':b['cellId'],'alphaBoundingBoxGap':round(gap,3),'guaranteedSilhouetteGap':gap>0})
report={'levelId':p['levelId'],'evidence':'actual BoardView source pieces and alpha128 atlas measurements; bounding-box overlap is not proof of connected stone connectors','unitCount':r['unitCount'],'buildingCount':r['buildingCount'],'wallCellCount':len(props),'counts':dict(collections.Counter(s['cellId'] for s in props)),'adjacencyMasksNESW':dict(sorted(masks.items())),'perAsset':{k:{'count':len([s for s in props if s['cellId']==k]),'logicalAlpha128Bounds':bs[k],'actualScaleRange':[min(s['effectiveScale'] for s in props if s['cellId']==k),max(s['effectiveScale'] for s in props if s['cellId']==k)],'catalogFootprint':next(s['footprint'] for s in props if s['cellId']==k)} for k in ids},'adjacentPairs':pairs,'pieces':props}
report['pairStats']={axis:{'pairs':len(v),'guaranteedGaps':sum(s['guaranteedSilhouetteGap'] for s in v),'gapMin':min(s['alphaBoundingBoxGap'] for s in v),'gapMax':max(s['alphaBoundingBoxGap'] for s in v)} for axis in ['east-west','north-south'] if (v:=[s for s in pairs if s['axis']==axis])}
(O/'runtime-fortification-measurements.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ['counts','adjacencyMasksNESW','pairStats']},indent=2))
print('central vertical pairs',*[s for s in pairs if s['first'][0] in [12,13] and s['axis']=='north-south'],sep='\n')
