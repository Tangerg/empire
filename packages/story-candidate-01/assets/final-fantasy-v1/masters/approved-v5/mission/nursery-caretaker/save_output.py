"""Persist unchanged built-in imagegen output; QA composites never replace native art."""
from pathlib import Path
import sys,json,shutil,hashlib
from PIL import Image
import numpy as np
from scipy.ndimage import distance_transform_edt
B=Path(__file__).parent
pose,native=sys.argv[1:3]; p=B/pose;p.mkdir(parents=True,exist_ok=True); native=Path(native)
for name in ['raw.png','master.png']:shutil.copy2(native,p/name)
im=Image.open(native); a=np.array(im.getchannel('A'));outside=distance_transform_edt(a<=127)>7
q={'nativeSize':list(im.size),'nativeMode':im.mode,'sha256':hashlib.sha256(native.read_bytes()).hexdigest(),'rawMatchesMaster':(p/'raw.png').read_bytes()==(p/'master.png').read_bytes(),'nativeOutputMatchesRaw':native.read_bytes()==(p/'raw.png').read_bytes(),'nativeAlphaUntouched':True,'alphaRange':[int(a.min()),int(a.max())],'alphaZeroFraction':float((a==0).mean()),'perimeterMaxAlpha':int(max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())),'exteriorBeyond7PixelsFromOpaqueSilhouette':{'pixelCountAlphaGreaterThan1':int(np.count_nonzero((a>1)&outside)),'maxAlpha':int(a[outside].max())}}
for t in [0,1,127]:
 y,x=np.where(a>t);q['bboxAlphaGreaterThan'+str(t)]=[int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]
for name,c in [('black',(0,0,0,255)),('white',(255,255,255,255))]:Image.alpha_composite(Image.new('RGBA',im.size,c),im).convert('RGB').save(p/f'qa-{name}.png')
(p/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
req=json.loads((p/'request.json').read_text());prov={'tool':'built-in image_gen.imagegen','requestSHA256':hashlib.sha256((p/'request.json').read_bytes()).hexdigest(),'promptSHA256':hashlib.sha256((p/'prompt.txt').read_bytes()).hexdigest(),'references':[{'path':x,'sha256':hashlib.sha256(Path(x).read_bytes()).hexdigest()} for x in req['referenced_image_paths']],'nativeAlphaUntouched':True,'rawMasterBytesIdentical':True,'postprocessing':'No cleanup, alpha editing, painting, crop, fit, or resize. Black/white compositing is QA-only, stored separately.'};(p/'request-provenance.json').write_text(json.dumps(prov,indent=2)+'\n')
print(json.dumps(q,indent=2))
