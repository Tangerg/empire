"""Offline current sprite support export; texture alpha is untouched.
Visible foundation band is manually identified, not generic sprite alpha-bottom
(would include non-physical generated residue). Each narrow strip owns its
actual lowest solid base texel bottom, not an ideal line through three corners.
"""
import json
from pathlib import Path
from PIL import Image
root=Path('/workspace/scratch/84c9fe948755/empire-c01/godot')
image=Image.open(root/'assets/native/c07-layered-v1/southern-supply-main-v1-candidate.png')
feet=[[262,680],[834,1012],[1278,837]]
def line(x):
 a,b=(feet[0],feet[1]) if x<=834 else (feet[1],feet[2])
 return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0])
strips=[];evidence=[]
for left in range(0,1536,8):
 right=min(1536,left+8);samples=[]
 for x in range(max(262,left),min(1279,right)):
  for y in range(max(0,int(line(x))-12),min(1024,int(line(x))+13)):
   if image.getpixel((x,y))[3]>=128:samples.append((x,y))
 if samples:
  x,y=max(samples,key=lambda p:p[1]);front=[x+.5,y+1.0]
  evidence.append({'span':[left,right],'physicalTexel':[x,y],'alpha':image.getpixel((x,y))[3],'front':front})
 else:
  x=(left+right-1)/2;front=[x,line(x)]
 strips.append({'rightPixels':right,'groundFrontPixels':front})
p=root/'data/scenes/c01-07-selected-assembly-objects.json';j=json.loads(p.read_text());asset=j['objects']['southern-supply-main-v1'];asset['anchorPixels']=[834,1013];asset['groundFrontPixels']=[834,1013];asset['depthStrips']=strips;p.write_text(json.dumps(j,indent=2)+'\n')
Path('/workspace/scratch/84c9fe948755/c07-static-preview/southern-supply-support-alpha-witness.json').write_text(json.dumps({'method':'Bottom boundary of solid base texels inside manually identified physical foundation band. Each 8px strip uses its deepest actual visible foot, at most 1.4 source-scene pixel breadth. Extrapolation outside foundation for roof overhang; texture and alpha unchanged.','manualFootBand':feet,'halfBandPixels':12,'solidTexelThreshold':128,'runtimeSupportsOwner':'selected object asset depthStrips','witnesses':evidence},indent=2)+'\n')
