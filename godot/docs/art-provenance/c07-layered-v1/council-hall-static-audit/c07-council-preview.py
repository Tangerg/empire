from pathlib import Path
from PIL import Image
import json,numpy as np,argparse
p=argparse.ArgumentParser();p.add_argument('--scale',type=float,required=True);p.add_argument('--tx',type=float,required=True);p.add_argument('--ty',type=float,required=True);args=p.parse_args()
r=Path('/workspace/scratch/84c9fe948755');g=r/'empire-c01/godot';s=args.scale;t=[args.tx,args.ty];anchor=[575,1065];target=[anchor[i]*s+t[i] for i in [0,1]]
a=np.asarray(Image.open(g/'assets/native/c07-layered-v1/council-hall-v1-candidate.png'))[:,:,3]
# Only the solid lower architectural foundation locates depth; image alpha is untouched.
profile=[[x,int(np.where(a[:,x]>=128)[0][-1])] for x in range(86,1381)]
def fy(x):
 if x<86:return profile[0][1]+(x-86)*(profile[100][1]-profile[0][1])/100
 if x>1380:return profile[-1][1]+(x-1380)*(profile[-1][1]-profile[-101][1])/100
 return profile[int(x)-86][1]
cuts=sorted({0,a.shape[1],86,575,850,930,1100,1180,1380,*range(0,a.shape[1],8)})
strips=[{'rightPixels':b,'groundFrontPixels':[(a+b-1)/2,fy((a+b-1)/2)]} for a,b in zip(cuts,cuts[1:])]
scene=json.load(open(g/'data/scenes/c01-07-granary-stock-preview.json'));scene['assembledGroundPath']='res://data/scenes/c01-07-council-preview-ground.json'
ground=json.load(open(g/'data/scenes/c01-07-granary-stock-preview-ground.json'));ground['objectCatalogPath']='res://data/scenes/c01-07-council-preview-objects.json';ground['objects'].append({'id':'council-hall-main','assetId':'council-hall-v1','contact':[q/16 for q in target],'scale':s,'selectionCell':[25,47],'ownsMapStructureBody':True,'pointer':'target'})
objects=json.load(open(g/'data/scenes/c01-07-granary-stock-preview-objects.json'));objects['objects']['council-hall-v1']={'resourcePath':'res://assets/native/c07-layered-v1/council-hall-v1-candidate.png','pixelSize':[a.shape[1],a.shape[0]],'anchorPixels':anchor,'groundFrontPixels':anchor,'contactShadowRadiiPixels':[35,6],'depthStrips':strips,'pointerPolygonPixels':[[900,665],[1035,625],[1035,844],[900,895]]}
for n,v in [('c01-07-council-preview.json',scene),('c01-07-council-preview-ground.json',ground),('c01-07-council-preview-objects.json',objects)]: (g/'data/scenes'/n).write_text(json.dumps(v,indent=2)+'\n')
v=r/'c07-readonly-review/council-runtime-registration';v.mkdir(exist_ok=True)
(v/'registration.json').write_text(json.dumps({'scope':'Unbound candidate. Hidden architecture reconstructed, original inner wood/stone witnesses not all external corners. Local foot profile separates right wing from forward porch.','scale':s,'translation':t,'anchor':anchor,'sourceAnchor':target,'bodyLowerProfile':profile,'virtualSideOverhangSupport':[[0,fy(0)],[a.shape[1]-1,fy(a.shape[1]-1)]],'stripCount':len(strips),'performancePassed':False},indent=2)+'\n')
print({'scale':s,'translation':t,'strips':len(strips)})
