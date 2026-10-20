from pathlib import Path
from PIL import Image
import sys,shutil,json,hashlib,numpy as np
B=Path(__file__).parent
pose,attempt,src=sys.argv[1:];d=B/pose;a=d/attempt;src=Path(src)
shutil.copy2(src,a/'raw.png')
for name in ['raw.png','master.png']:shutil.copy2(src,d/name)
for name in ['prompt.txt','request.json','request-provenance.json']:shutil.copy2(a/name,d/name)
im=Image.open(src);arr=np.array(im);assert im.mode=='RGBA';alpha=arr[:,:,3];edge=np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]])
qa={'nativeSource':str(src),'nativeSize':list(im.size),'nativeMode':im.mode,'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'bboxAlphaNonzero':im.getchannel('A').getbbox(),'bboxAlphaGreaterThan127':Image.fromarray((alpha>127).astype('uint8')*255).getbbox(),'perimeterMaxAlpha':int(edge.max()),'perimeterCountAlphaGreaterThan1':int((edge>1).sum()),'alphaZeroFraction':float((alpha==0).mean()),'alphaRange':[int(alpha.min()),int(alpha.max())],'rawMatchesMaster':(d/'raw.png').read_bytes()==(d/'master.png').read_bytes(),'nativeAlphaUntouched':True,'postprocessing':'none; immutable native raw and master copied byte-for-byte'}
for name,col in [('black',(16,16,16)),('white',(255,255,255)),('scene',(114,128,86))]:
 bg=Image.new('RGBA',im.size,col+(255,));bg.alpha_composite(im);bg.convert('RGB').save(d/f'qa-{name}.png')
res={'nativeSource':str(src),'tool':'built-in image_gen.imagegen','selected':True,'sha256':qa['sha256'],'promptSha256':hashlib.sha256((d/'prompt.txt').read_bytes()).hexdigest(),'nativeMatchesSavedRaw':True,'actualNativeCanvas':im.size}
for p in [a,d]:(p/'generation-result.json').write_text(json.dumps(res,indent=2)+'\n')
(d/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n');print(json.dumps(qa,indent=2))
