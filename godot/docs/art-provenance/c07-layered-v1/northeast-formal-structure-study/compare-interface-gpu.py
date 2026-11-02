"""Only direct Godot RGBA PNG readbacks qualify as numerical pixel evidence."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import argparse,hashlib,json
import numpy as np
root=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--baseline',type=Path,default=root/'gpu-main-baseline.png');parser.add_argument('--candidate',type=Path,default=root/'gpu-main-interface.png');args=parser.parse_args()
import sys
tool_root = next((parent / 'tools/godot' for parent in root.parents
                  if (parent / 'tools/godot/native_image_oracle.py').is_file()),
                 root.parent / 'empire-c01/tools/godot')
sys.path.insert(0, str(tool_root))
from native_image_oracle import read_native_rgba_png as rgba_png
a,ai=rgba_png(args.baseline);b,bi=rgba_png(args.candidate)
if a.shape!=b.shape:raise ValueError('Framebuffer dimensions differ')
projection=json.loads((root/'preview-projection.json').read_text());source=json.loads((root/'northeast-frozen-v2-sections.json').read_text());cut=json.loads((root/'northeast-interface-design-contract.json').read_text())['replacementDomainTexturePixels']
poly=[[(v[i]*source['scale']+source['translation'][i])*projection['scale']+projection['position'][i] for i in range(2)]for v in cut]
mask=Image.new('L',(a.shape[1],a.shape[0]));ImageDraw.Draw(mask).polygon([tuple(v)for v in poly],fill=255);outside=np.array(mask.filter(ImageFilter.MaxFilter(5)))==0;outside[:72]=False
d=np.abs(a.astype(int)-b.astype(int));changed=np.any(d!=0,axis=2);changed[:72]=False
r={'scope':'Direct Godot GetViewport Texture Image SavePng, checked PNG magic and RGBA; same native2.5 camera.72px caption excluded; explicit replacement-domain2px raster halo.', 'inputs':[ai,bi], 'changedPixels':int(changed.sum()),'outsideDomainChangedPixels':int((changed&outside).sum()),'outsideMaxChannelDelta':int(d[outside].max()),'outsideNewZeroAlphaPixels':int(((a[:,:,3]>0)&(b[:,:,3]==0)&outside).sum()),'rasterHaloClientPixels':2}
(root/'interface-native-lossless-gpu-result.json').write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r,indent=2))
