from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib,shutil,sys
root=Path('/workspace/shared/empire-art-rebuild/approved-v5/mission/bridge-laborer')
pose,source=sys.argv[1:3]; out=root/pose;out.mkdir(exist_ok=True,parents=True)
for name in ('raw.png','master.png'):
 p=out/name
 if p.exists(): raise SystemExit('Refusing native overwrite '+str(p))
 shutil.copy2(source,p)
im=Image.open(out/'master.png'); a=np.asarray(im.getchannel('A')); opaque=a>127; ys,xs=np.where(opaque); b=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]
edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
report={'nativeSource':source,'mode':im.mode,'size':im.size,'sha256':hashlib.sha256((out/'master.png').read_bytes()).hexdigest(),'rawEqualsMaster':(out/'raw.png').read_bytes()==(out/'master.png').read_bytes(),'alphaMinMax':[int(a.min()),int(a.max())],'transparentPixelCount':int((a==0).sum()),'opaquePixelCount':int((a==255).sum()),'partialAlphaCount':int(((a>0)&(a<255)).sum()),'visibleAlpha127Bounds':b,'allNonzeroAlphaBounds':im.getchannel('A').getbbox(),'perimeterMaxAlpha':int(edge.max()),'perimeterPixelsAlphaAbove1':int((edge>1).sum()),'noPixelChanges':True}
for name,color in [('white',(255,255,255,255)),('black',(0,0,0,255))]:
 bg=Image.new('RGBA',im.size,color); bg.alpha_composite(im); bg.convert('RGB').save(out/f'qa-{name}.png')
json.dump(report,(out/'alpha-qa.json').open('w'),indent=2)
print(json.dumps(report,indent=2))
