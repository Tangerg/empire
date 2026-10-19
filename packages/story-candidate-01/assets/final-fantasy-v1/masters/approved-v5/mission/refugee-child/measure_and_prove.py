from PIL import Image,ImageDraw
from pathlib import Path
import numpy as np,json,hashlib
D=Path('/workspace/shared/empire-art-rebuild/approved-v5/mission/refugee-child')
for pose in ['idle','walk-a','carry-or-work','walk-b']:
 p=D/pose/'master.png'
 if not p.exists():continue
 im=Image.open(p).convert('RGBA');a=np.asarray(im)[:,:,3]; edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 q={'size':im.size,'mode':Image.open(p).mode,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'rawEqualsMaster':p.read_bytes()==(D/pose/'raw.png').read_bytes(),'alphaMinimum':int(a.min()),'alphaMaximum':int(a.max()),'fullyTransparentPixels':int((a==0).sum()),'maxNativePerimeterAlpha':int(edge.max()),'pixelsWithPerimeterAlphaGreaterThan1':int((edge>1).sum()),'passesNativeSilhouettePerimeterGate':bool((edge<=1).all()),'allNonzeroAlphaBounds':im.getbbox(),'visibleBoundsAlphaGreaterThan127':Image.fromarray((a>127).astype('uint8')*255).getbbox(),'nativePolicy':'Raw and master byte-identical, generated alpha preserved; alpha1 dust not removed. Proof composites only.'}
 for color,name in [((0,0,0,255),'black'),((255,255,255,255),'white')]:
  b=Image.new('RGBA',im.size,color);b.alpha_composite(im);b.convert('RGB').save(D/pose/f'qa-{name}.png')
 (D/pose/'alpha-qa.json').write_text(json.dumps(q,indent=2));print(pose,json.dumps(q))
