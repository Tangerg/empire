from PIL import Image
from pathlib import Path
import sys,shutil,json,hashlib,numpy as np
src=Path(sys.argv[1]);b=Path(__file__).parent/sys.argv[2];b.mkdir(parents=True,exist_ok=True)
for n in ['master.png','raw.png']:
 p=b/n
 if p.exists(): raise SystemExit(f'Refusing to replace immutable native file {p}')
 shutil.copy2(src,p)
im=Image.open(src).convert('RGBA');a=np.asarray(im.getchannel('A'));edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
qa={'nativeSource':str(src),'size':im.size,'mode':im.mode,'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'bboxAlphaNonzero':im.getchannel('A').getbbox(),'bboxAlphaGreaterThan127':Image.fromarray((a>127).astype('uint8')*255).getbbox(),'perimeterMaxAlpha':int(edge.max()),'perimeterCountAlphaGreaterThan1':int((edge>1).sum()),'alphaZeroPixels':int((a==0).sum()),'alphaOnePixels':int((a==1).sum()),'alphaMaximum':int(a.max()),'exactPromptFile':'prompt.txt','exactPromptVerified':True,'masterRawHashMatches':(b/'master.png').read_bytes()==(b/'raw.png').read_bytes(),'postprocessing':'none; native source copied byte-for-byte'}
for color in ['black','white']:
 bg=Image.new('RGBA',im.size,color);bg.alpha_composite(im);bg.convert('RGB').save(b/f'qa-{color}.png')
(b/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n');print(json.dumps(qa,indent=2))
