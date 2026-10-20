from pathlib import Path
from PIL import Image
import json,hashlib,shutil,sys,numpy as np
from scipy.ndimage import distance_transform_edt
B=Path(__file__).parent
pose,src=sys.argv[1:3];p=B/pose;p.mkdir(exist_ok=True)
for name in ['raw.png','master.png']:shutil.copy2(src,p/name)
im=Image.open(p/'master.png');a=np.array(im.getchannel('A'));edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def box(t):
 y,x=np.where(a>t);return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)] if len(x) else None
q={'nativeSize':list(im.size),'mode':im.mode,'rawSha256':sha(p/'raw.png'),'masterSha256':sha(p/'master.png'),'rawMasterIdentical':sha(p/'raw.png')==sha(p/'master.png'),'nativeOutputMatchesRaw':Path(src).read_bytes()==(p/'raw.png').read_bytes(),'nativeAlphaUntouched':True,'bboxAlphaGreaterThan0':box(0),'bboxAlphaGreaterThan1':box(1),'bboxAlphaGreaterThan127':box(127),'perimeterMaxAlpha':int(edge.max()),'perimeterCountAlphaGreaterThan1':int((edge>1).sum()),'alphaCountsAboveThreshold':{str(t):int((a>t).sum()) for t in [0,1,16,64,127,240,250,254]},'exteriorBeyond7PixelsFromOpaqueSilhouette':int(a[distance_transform_edt(a<128)>7].max()),'sourcePolicy':'Native raw/master unchanged. No cleanup, paint, crop, alpha editing, per-pose scaling, or source padding. Separate QA composites only.'}
(p/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
(p/'generation-result.json').write_text(json.dumps({'tool':'built-in image_gen.imagegen','nativeOutputPath':src,'transparent_background':True,'nativeSize':list(im.size)},indent=2)+'\n')
r=json.loads((p/'request.json').read_text());(p/'request-provenance.json').write_text(json.dumps({'tool':'built-in image_gen.imagegen','promptFileSha256':sha(p/'prompt.txt'),'requestFileSha256':sha(p/'request.json'),'references':[{'path':s,'sha256':sha(Path(s))} for s in r['referenced_image_paths']],'rawSha256':q['rawSha256'],'masterSha256':q['masterSha256']},indent=2)+'\n')
for name,c in {'black':(0,0,0,255),'white':(255,255,255,255),'scene':(109,126,93,255)}.items():
 bg=Image.new('RGBA',im.size,c);bg.alpha_composite(im);bg.convert('RGB').save(p/f'qa-{name}.png')
print(json.dumps(q,indent=2))
