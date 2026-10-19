from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,shutil,sys
BASE=Path(__file__).resolve().parent

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def record(pose,source,refs):
 d=BASE/pose;d.mkdir(parents=True,exist_ok=True);source=Path(source)
 for name in ['raw.png','master.png']:
  target=d/name
  if target.exists():assert sha(target)==sha(source),'Never overwrite a different native output'
  else:shutil.copy2(source,target)
  target.chmod(0o444)
 im=Image.open(d/'master.png');assert im.mode=='RGBA';a=np.array(im)[:,:,3]
 bounds={}
 for threshold in [0,1,127,250]:
  yy,xx=np.where(a>threshold);bounds[str(threshold)]=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
 hist=np.bincount(a.ravel(),minlength=256)
 qa={'nativeSize':list(im.size),'mode':im.mode,'visibleBoundsByAlphaGreaterThan':bounds,'alphaZeroPixels':int(hist[0]),'alphaOnePixels':int(hist[1]),'alphaAtLeast250Pixels':int(hist[250:].sum()),'alpha255Pixels':int(hist[255]),'alphaHistogram':hist.tolist(),'perimeterMaxAlpha':int(max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())),'masterSha256':sha(d/'master.png'),'rawSha256':sha(d/'raw.png'),'rawEqualsMaster':sha(d/'raw.png')==sha(d/'master.png'),'policy':'Native imagegen RGBA untouched. Background composites and scaling previews are QA only. No alpha cleanup, cropping, recoloring or art modification.'}
 (d/'alpha-qa.json').write_text(json.dumps(qa,indent=2)+'\n')
 for name,c in [('black',(0,0,0,255)),('white',(255,255,255,255)),('gray',(98,110,100,255))]:
  bg=Image.new('RGBA',im.size,c);bg.alpha_composite(im);bg.convert('RGB').save(d/f'qa-{name}.png')
 prompt=(d/'prompt.txt').read_text().rstrip('\n')
 request={'prompt':prompt,'referenced_image_paths':refs,'transparent_background':True}
 (d/'request.json').write_text(json.dumps(request,indent=2)+'\n')
 (d/'provenance.json').write_text(json.dumps({'generationTool':'built-in image_gen','generationSource':str(source),'sourceSha256':sha(source),'nativeSize':list(im.size),'mode':im.mode,'raw':'raw.png','master':'master.png','rawEqualsMaster':True,'promptSha256':hashlib.sha256(prompt.encode()).hexdigest(),'request':'request.json','transformations':[]},indent=2)+'\n')
 return qa
if __name__=='__main__':print(json.dumps(record(sys.argv[1],sys.argv[2],sys.argv[3:]),indent=2))
