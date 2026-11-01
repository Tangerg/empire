from pathlib import Path
from PIL import Image
import json,numpy as np
r=Path('/workspace/scratch/84c9fe948755');g=r/'empire-c01/godot'
s=.17;anchor=[1150,958];target=[579,180];t=[target[i]-s*anchor[i] for i in range(2)]
a=np.asarray(Image.open(g/'assets/native/c07-layered-v1/granary-main-v1-candidate.png'))[:,:,3]
# Real visible lower foundation, not shadow or upper roof fringe. Alpha only locates the
# authored solid boundary; rendering keeps the untouched PNG alpha, including antialiasing.
profile=[]
for x in range(130,1281):
 ys=np.where(a[:,x]>=128)[0];profile.append([x,int(ys[-1])])
def fy(x):
 if x<130:return profile[0][1]+(x-130)*(1060-profile[0][1])/(775-130)
 if x>1280:return profile[-1][1]+(x-1280)*(profile[-1][1]-1060)/(1280-775)
 return profile[int(x)-130][1]
cuts=sorted({0,1374,*range(0,1374,8),130,775,935,975,1100,1150,1280})
strips=[{'rightPixels':b,'groundFrontPixels':[(a+b-1)/2,fy((a+b-1)/2)]} for a,b in zip(cuts,cuts[1:])]
scene=json.load(open(g/'data/scenes/c01-07-front-stock-preview.json'));scene['assembledGroundPath']='res://data/scenes/c01-07-granary-preview-ground.json'
ground=json.load(open(g/'data/scenes/c01-07-front-stock-preview-ground.json'));ground['objectCatalogPath']='res://data/scenes/c01-07-granary-preview-objects.json';ground['objects'].append({'id':'granary-main','assetId':'granary-main-v1','contact':[q/16 for q in target],'scale':s,'selectionCell':[35,14],'ownsMapStructureBody':True,'pointer':'target'})
objects=json.load(open(g/'data/scenes/c01-07-front-stock-preview-objects.json'));objects['objects']['granary-main-v1']={'resourcePath':'res://assets/native/c07-layered-v1/granary-main-v1-candidate.png','pixelSize':[1374,1145],'anchorPixels':anchor,'groundFrontPixels':anchor,'contactShadowRadiiPixels':[35,6],'depthStrips':strips,'pointerPolygonPixels':[[920,1015],[920,811],[952,730],[1000,694],[1058,689],[1111,717],[1150,791],[1150,957],[1103,947],[974,986],[974,1015]]}
for n,v in [('c01-07-granary-preview.json',scene),('c01-07-granary-preview-ground.json',ground),('c01-07-granary-preview-objects.json',objects)]: (g/'data/scenes'/n).write_text(json.dumps(v,indent=2)+'\n')
v=r/'c07-readonly-review/granary-main-candidate-v1';v.mkdir(exist_ok=True)
(v/'registration.json').write_text(json.dumps({'scope':'Unbound candidate, source certified door right stone foot only; hidden outer corners not certified','sourceScale':s,'sourceTranslation':t,'doorFootAsset':anchor,'doorFootSource':target,'bodyFoundationProfile':profile,'virtualOverhangSupportEnds':[[0,fy(0)],[1373,fy(1373)]],'depthStripCount':len(strips),'performancePassed':False},indent=2)+'\n')
print({'scale':s,'translation':t,'strips':len(strips)})
