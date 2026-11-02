from pathlib import Path
import json,math,ast,hashlib
import numpy as np
out=Path(__file__).resolve().parent;root=out.parents[2];repo=root/'empire-c01';sp=root/'c07-static-preview';files=[out.parent/'trellis-final-parallel-base-review/front-source-frozen-input.json',out.parent/'trellis-final-parallel-base-review/back-source-frozen-input.json'];sources=[json.loads(p.read_text()) for p in files];houses=json.loads((root/'c07-readonly-review/blue-house-foot-study/blue-v2-whole-new-base-and-orange-separation.json').read_text());wall=json.loads((out.parent/'stone-v2-foot-study/v2-full-thin-wall-footing-review.json').read_text());frozen=json.loads((root/'c07-static-preview/central-east-planter-review/rear-white-planter-new-base-walk-corridor.json').read_text());level=next(v for v in json.loads((repo/'godot/data/grayflag-campaign-levels.json').read_text()) if v['id']=='c01-07')
s=root/'c07-static-preview/central-rear-west-bed-review/verify-rear-west-actual-four-leg-footing.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name in ['inside','cross','area','intersection','cc','ptseg']],type_ignores=[]),str(s),'exec'));s=out.parent/'stone-v2-foot-study/verify-thin-stone-wall.py';m=ast.parse(s.read_text());exec(compile(ast.Module(body=[v for v in m.body if isinstance(v,ast.FunctionDef) and v.name=='boundary_scan'],type_ignores=[]),str(s),'exec'))
A=[];B=[];meta=[]
for a,b in wall['directedEdges']:
 for west in ([False,True] if a[0]==b[0] else [b[0]<a[0]]):
  for f in frozen['frames']:
   for v in f['sole']:
    off=[(v[0]-f['anchor'][0])*frozen['scale']*(-1 if west else 1),(v[1]-f['anchor'][1])*frozen['scale']];A.append([16*a[k]+8+off[k] for k in range(2)]);B.append([16*b[k]+8+off[k] for k in range(2)]);meta.append({'fromCell':a,'toCell':b,'west':west,'frame':f['index'],'soleFramePixel':v})
A=np.array(A);B=np.array(B)
others={'orange':houses['orangeNewClosedBase']['polygon'],'blue':houses['blueNewClosedBase']['polygon'],'stoneWall':wall['newThinGroundBase']['polygon']}
def check(poly):
 lo=np.min(poly,axis=0);hi=np.max(poly,axis=0);cells=[]
 for y in range(int(lo[1]//16),int(hi[1]//16)+1):
  for x in range(int(lo[0]//16),int(hi[0]//16)+1):
   p=intersection(poly,[[x*16,y*16],[x*16+16,y*16],[x*16+16,y*16+16],[x*16,y*16+16]]);v=area(p)
   if v>1e-8:cells.append({'cell':[x,y],'terrain':level['terrain'][y][x],'area':v})
 walk=boundary_scan(A,B,poly,meta);ids=np.flatnonzero((np.all(A>=lo,axis=1)&np.all(A<=hi,axis=1))|(np.all(B>=lo,axis=1)&np.all(B<=hi,axis=1)));insideIds=[int(i) for i in ids if inside(A[i],poly) or inside(B[i],poly)];relations={}
 for name,P in others.items():
  Q=np.array(P);R=np.roll(Q,-1,axis=0);scan=boundary_scan(Q,R,poly,[{'otherBoundaryEdge':i} for i in range(len(Q))]);i1=[i for i,p in enumerate(poly) if inside(p,P)];i2=[i for i,p in enumerate(P) if inside(p,poly)];cut=intersection(P,poly)
  relations[name]={'boundary':scan,'postVerticesInsideOther':i1,'otherVerticesInsidePost':i2,'intersectionArea':area(cut),'intersectionPolygon':cut,'disjoint':not i1 and not i2 and scan['intersectingSegments']==0}
 return {'footPolygon':poly,'positiveAreaCells':cells,'walk':walk,'walkEndpointsInside':len(insideIds),'relations':relations}

from PIL import Image
p=root/'generated_images/exec-7c9226b7-3396-401f-8ef4-cea56eeb05f9.png';im=Image.open(p).convert('RGBA');assert im.size==(1024,1536)
foot=[]
for x in range(510,584):
 ys=[y for y in range(1460,1487) if im.getpixel((x,y))[3]>=244]
 assert ys,x
 y=max(ys);foot.append([x,y])
# Only the visually identified terminal woody root's lower contact band is sampled.
# These two rear points close a small NEW footprint; material at them is wood, but no original hidden rear ground claim.
rear=[[573,1460],[528,1460]];local=foot+rear
final=json.loads((out.parent/'trellis-final-parallel-base-review/final-parallel-rear-row-footing-review.json').read_text())
for name,v in final['finiteFeet'].items():others[name]=v['footPolygon']
soil=[[552,448.5],[580,434.5],[575,432],[547,446]]
instances=[]
for name,scale,oldcontact,newcontact in [('near',.02,[556,444],[556.1,444]),('far',.018,[573,435],[573.09,435])]:
 origin=[newcontact[0]-570*scale,newcontact[1]-1486*scale];oldorigin=[oldcontact[0]-565*scale,oldcontact[1]-1486*scale];assert all(abs(a-b)<1e-10 for a,b in zip(origin,oldorigin))
 world=[[origin[0]+x*scale,origin[1]+y*scale] for x,y in local];r=check(world)
 for rel in r['relations'].values():
  # Clipping an arbitrary other floor by a concave root contour is not valid Sutherland-Hodgman; only exact boundary + both-way inside used.
  rel.pop('intersectionArea');rel.pop('intersectionPolygon')
 Q=np.array(soil);soilscan=boundary_scan(Q,np.roll(Q,-1,axis=0),world,[{'soilEdge':i} for i in range(len(soil))])
 r['soilContainment']={'allFootVerticesStrictlyInside':all(inside(v,soil) for v in world),'boundary':soilscan,'soilConvex':True}
 r.update({'id':name,'scale':scale,'oldAnchor':[565,1486],'oldContact':oldcontact,'anchor':[565,1486],'contact':oldcontact,'groundFrontPixels':[570,1486],'actualGroundFrontWorld':newcontact,'registrationClarification':'Final implementation retains non-solid anchor5651486; solid groundFront5701486 is separate; numerical origin and all geometry unchanged.','origin':origin,'geometryUnchanged':True,'actualVisibleLowerFoot':world[:len(foot)],'newHiddenRearClosure':world[len(foot):]});instances.append(r)
P=np.array(instances[0]['footPolygon']);inter=boundary_scan(P,np.roll(P,-1,axis=0),instances[1]['footPolygon'],[{'rootEdge':i} for i in range(len(P))])
report={'ready':True,'scope':'Only two registered terminal woody roots, real lower material contour + explicitly NEW small rear closure; no foliage alpha bbox and no source hidden-foot claim. Static no engine/GUI/production changes.','asset':{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'size':list(im.size)},'rootCropSourceRect':[470,1400,620,1505],'actualVisibleLowerFootAssetPixels':[{'pixel':v,'rgba':list(im.getpixel(tuple(v)))} for v in foot],'newHiddenRearAssetClosure':[{'pixel':v,'rgba':list(im.getpixel(tuple(v))),'status':'NEW rear ground closure, opaque wood at source pixel is not proof of hidden physical foot'} for v in rear],'anchorEvidence':{'old5651486':list(im.getpixel((565,1486))),'new5701486':list(im.getpixel((570,1486)))},'soilPolygon':soil,'instances':instances,'betweenRoots':inter,'walkSegments':len(A),'limits':['Exact alpha threshold applied only after manual material identification within bounded terminal-root ROI.','Lower visible root boundary is observed; closed rear footprint is a small NEW design assumption.','Frozen local Torren walk-a/b sole corridors reused; no all-role sweep, gameplay or actual depth acceptance.','Trunk/foliage projection overlap with trellis or wall is not ground-volume intersection.']}
(out/'two-rooted-vine-foot-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'ready':True,'lowerSamples':len(foot),'instances':[{'id':v['id'],'origin':v['origin'],'cells':v['positiveAreaCells'],'soilInside':v['soilContainment']['allFootVerticesStrictlyInside'],'soilGap':v['soilContainment']['boundary']['minimumDistance'],'walkGap':v['walk']['minimumDistance'],'walkCross':v['walk']['intersectingSegments'],'walkInside':v['walkEndpointsInside'],'relations':{n:{'gap':r['boundary']['minimumDistance'],'disjoint':r['disjoint']} for n,r in v['relations'].items()}} for v in instances],'rootGap':inter['minimumDistance']},indent=2))
