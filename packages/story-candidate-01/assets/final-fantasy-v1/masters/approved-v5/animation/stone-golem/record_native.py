"""Byte-preserve built-in imagegen sources and create inspection-only QA derivatives."""
from pathlib import Path
from PIL import Image
import sys,json,hashlib,shutil,numpy as np
root=Path(__file__).parent
pose,source=sys.argv[1:3]
d=root/pose;d.mkdir(parents=True,exist_ok=True);src=Path(source)
for name in ('raw.png','master.png'):
 target=d/name
 if target.exists(): raise SystemExit(f'Refusing to overwrite existing {target}')
 shutil.copy2(src,target)
im=Image.open(src)
if im.mode!='RGBA': raise SystemExit(f'Native alpha missing: {im.mode}')
a=np.array(im)[:,:,3]
def bbox(threshold):
 yy,xx=np.where(a>threshold)
 return [int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
q={'nativeSource':str(src),'size':im.size,'mode':im.mode,'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'bboxAlphaNonzero':bbox(0),'bboxAlphaGreaterThan127':bbox(127),'perimeterMaxAlpha':int(edge.max()),'perimeterCountAlphaGreaterThan1':int((edge>1).sum()),'alphaZeroPixels':int((a==0).sum()),'alphaOnePixels':int((a==1).sum()),'postprocessing':'none; native source copied byte-for-byte','prompt':'prompt.txt'}
(d/'alpha-qa.json').write_text(json.dumps(q,indent=2))
for color,name in [((0,0,0,255),'black'),((255,255,255,255),'white')]:
 bg=Image.new('RGBA',im.size,color);bg.alpha_composite(im);bg.convert('RGB').save(d/f'qa-{name}.png')
refs=[root/'idle/master.png']
r={'tool':'image_gen.imagegen','mode':'built-in','transparent_background':True,'promptFile':'prompt.txt','promptSha256':hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest(),'referenced_image_paths':[str(x) for x in refs],'referenceSha256':{str(x):hashlib.sha256(x.read_bytes()).hexdigest() for x in refs},'nativeSource':str(src),'noPostprocessing':True}
(d/'request.json').write_text(json.dumps(r,indent=2));print(json.dumps(q,indent=2))
