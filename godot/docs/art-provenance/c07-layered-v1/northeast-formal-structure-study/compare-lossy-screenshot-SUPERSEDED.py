from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import numpy as np,json
p=Path(__file__).resolve().parent;a=np.array(Image.open(p/'baseline-same-dll-A-1180.png').convert('RGBA'));b=np.array(Image.open(p/'interface-affine-1180.png').convert('RGBA'));assert a.shape==b.shape
projection=json.load(open(p/'preview-projection.json'));source=json.load(open(p/'northeast-frozen-v2-sections.json'));cut=json.load(open(p/'northeast-interface-design-contract.json'))['replacementDomainTexturePixels']
poly=[[(v[i]*source['scale']+source['translation'][i])*projection['scale']+projection['position'][i] for i in range(2)] for v in cut]
mask=Image.new('L',(a.shape[1],a.shape[0]));ImageDraw.Draw(mask).polygon([tuple(v) for v in poly],fill=255);m=np.array(mask.filter(ImageFilter.MaxFilter(5)))>0;d=np.abs(a.astype(int)-b.astype(int));changed=np.any(d!=0,axis=2);changed[:72]=False;outside=changed&~m;ys,xs=np.where(changed)
r={'scope':'Same native2.5 camera/world framebuffer;72px caption excluded. Explicit interface domain plus2client-pixel AA halo. No zero-difference claim unless measured.', 'changedPixels':int(changed.sum()),'outsideDomainHaloChanged':int(outside.sum()),'outsideMaxChannelDelta':int(d[outside].max()) if outside.any() else 0,'maxChannelDelta':int(d[72:].max()),'changedBounds':[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())],'projectedDomain':poly,'rasterHaloClientPixels':2,'outsideDeltaHistogram':{str(i):int(((d.max(2)>i)&outside).sum()) for i in [0,1,2,5,20]}}
(p/'interface-affine-gpu-diff.json').write_text(json.dumps(r,indent=2)+'\n');print(r)
