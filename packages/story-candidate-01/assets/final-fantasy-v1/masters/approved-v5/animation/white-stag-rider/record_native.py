from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,shutil,sys
r=Path(__file__).parent;pose,src=sys.argv[1:3];p=r/pose;p.mkdir(parents=True,exist_ok=True);src=Path(src);shutil.copyfile(src,p/'raw.png');shutil.copyfile(src,p/'master.png');im=Image.open(src);a=np.asarray(im.getchannel('A'));edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
q={'source':str(src),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'size':list(im.size),'mode':im.mode,'alphaNonzeroBounds':im.getchannel('A').getbbox(),'alphaGreater127Bounds':im.getchannel('A').point(lambda x:255 if x>127 else 0).getbbox(),'perimeterMaxAlpha':int(edge.max()),'perimeterPixelsAlphaGreaterThan1':int((edge>1).sum()),'alphaZeroPixels':int((a==0).sum()),'alphaOnePixels':int((a==1).sum()),'postprocessing':'None: native raw and master byte-identical tool output'}
for c,n in [((0,0,0,255),'black'),((255,255,255,255),'white')]:
 bg=Image.new('RGBA',im.size,c);bg.alpha_composite(im);bg.convert('RGB').save(p/f'qa-{n}.png')
(p/'alpha-qa.json').write_text(json.dumps(q,indent=2));print(json.dumps(q,indent=2))
