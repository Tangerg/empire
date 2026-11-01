from pathlib import Path
from PIL import Image
import numpy as np,json
r=Path('/workspace/scratch/84c9fe948755');g=r/'empire-c01/godot';a=np.asarray(Image.open(g/'assets/native/c07-layered-v1/granary-large-grain-v1-candidate.png'))[:,:,3]
s=.043;t=[355.75,167.24];anchor=[750,1121];contact=[anchor[i]*s+t[i] for i in [0,1]]
profile=[[x,int(np.where(a[:,x]>=128)[0][-1])] for x in range(95,1231)]
def fy(x):
 if x<95:return profile[0][1]+(x-95)*(profile[100][1]-profile[0][1])/100
 if x>1230:return profile[-1][1]+(x-1230)*(profile[-1][1]-profile[-101][1])/100
 return profile[int(x)-95][1]
cuts=sorted({0,a.shape[1],95,750,1230,*range(0,a.shape[1],8)})
strips=[{'rightPixels':b,'groundFrontPixels':[(a+b-1)/2,fy((a+b-1)/2)]} for a,b in zip(cuts,cuts[1:])]
scene=json.load(open(g/'data/scenes/c01-07-granary-preview.json'));scene['assembledGroundPath']='res://data/scenes/c01-07-granary-stock-preview-ground.json'
ground=json.load(open(g/'data/scenes/c01-07-granary-preview-ground.json'));ground['objectCatalogPath']='res://data/scenes/c01-07-granary-stock-preview-objects.json';ground['objects'].append({'id':'granary-large-grain','assetId':'granary-large-grain-v1','contact':[x/16 for x in contact],'scale':s,'selectionCell':None,'pointer':'pass-through','ownsMapStructureBody':False})
objects=json.load(open(g/'data/scenes/c01-07-granary-preview-objects.json'));objects['objects']['granary-large-grain-v1']={'resourcePath':'res://assets/native/c07-layered-v1/granary-large-grain-v1-candidate.png','pixelSize':[a.shape[1],a.shape[0]],'anchorPixels':anchor,'groundFrontPixels':anchor,'contactShadowRadiiPixels':[20,3],'depthStrips':strips}
for n,v in [('c01-07-granary-stock-preview.json',scene),('c01-07-granary-stock-preview-ground.json',ground),('c01-07-granary-stock-preview-objects.json',objects)]: (g/'data/scenes'/n).write_text(json.dumps(v,indent=2)+'\n')
pts=[]
for X,Y in [[388,215],[394,213],[396,212],[402,185],[403,185],[388,210],[390,211],[402,178],[414,208],[396,216],[406,221],[424,216]]:
 x,y=round((X-t[0])/s),round((Y-t[1])/s);alpha=int(a[y,x]) if 0<=x<a.shape[1] and 0<=y<a.shape[0] else 0
 pts.append({'source':[X,Y],'asset':[x,y],'alpha':alpha})
v=r/'c07-readonly-review/granary-bale-candidate-v1';v.mkdir(exist_ok=True)
(v/'registration.json').write_text(json.dumps({'scope':'Source visible foot samples and below-ground exclusions retained; new hidden east support not original proof. Stock entire new silhouette is bounded reconstruction, not pixel identity.','scale':s,'translation':t,'anchor':anchor,'sourceAnchor':contact,'bodyLowerProfile':profile,'virtualSideOverhangSupport':[[0,fy(0)],[1286,fy(1286)]],'nativeStripCount':len(strips),'samples':pts,'knownBelowOriginalFeet':[[388,217],[394,215],[396,214]],'sourceEast402FootKnown':False,'designEast402FootApprox':fy((402-t[0])/s)*s+t[1],'oldBackRoleBugClaim':False,'alphaRenderAltered':False},indent=2)+'\n')
print({'anchor':contact,'strips':len(strips),'samples':pts})
