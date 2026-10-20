from pathlib import Path
from PIL import Image
import sys,shutil,hashlib,json,numpy as np
root=Path(__file__).parent;phase,generated=sys.argv[1:];d=root/phase;d.mkdir(exist_ok=True);p=Path(generated)
for name in ['raw.png','master.png']:shutil.copy2(p,d/name)
im=Image.open(p);a=np.array(im.getchannel('A'));edges=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
r={'phase':phase,'tool':'built-in image_gen','generatedSource':str(p),'sourceCanvas':list(im.size),'mode':im.mode,'sha256':sha(p),'decodedRgbaSha256':hashlib.sha256(im.tobytes()).hexdigest(),'edgeMax':int(edges.max()),'edgeNonzero':int(np.count_nonzero(edges)),'alphaMax':int(a.max()),'partialAlphaCount':int(np.count_nonzero((a>0)&(a<255))),'bboxAlpha1':im.getchannel('A').getbbox(),'bboxAlpha128':Image.fromarray((a>=128).astype('uint8')).getbbox(),'rawMasterByteIdentical':True,'alphaCleanup':False}
(d/'provenance.json').write_text(json.dumps(r,indent=2)+'\n');(d/'hashes.json').write_text(json.dumps({'raw':sha(d/'raw.png'),'master':sha(d/'master.png'),'decodedRgba':r['decodedRgbaSha256']},indent=2)+'\n')
(d/'source-evidence.json').write_text(json.dumps({'topicId':'C01-FX-12','phase':phase,'sourceBoard':'fx-atlas-03.png','sourceRow1Based':4,'audit':'../source-evidence.json','auditSha256':sha(root/'source-evidence.json'),'referenceHashes':'reference-hashes.json'},indent=2)+'\n');print(json.dumps(r,indent=2))
