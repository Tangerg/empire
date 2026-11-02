from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import json,sys,numpy as np
root=Path(__file__).resolve().parent
tool_root = next((parent / 'tools/godot' for parent in root.parents
                  if (parent / 'tools/godot/native_image_oracle.py').is_file()),
                 root.parent / 'empire-c01/tools/godot')
sys.path.insert(0, str(tool_root))
from native_image_oracle import read_native_rgba_png
left,li=read_native_rgba_png(root/'gpu-alpha-baseline.png');right,ri=read_native_rgba_png(root/'gpu-alpha-interface.png')
assert left.shape==right.shape
projection=json.loads((root/'preview-projection.json').read_text());source=json.loads((root/'northeast-frozen-v2-sections.json').read_text());cut=json.loads((root/'northeast-interface-design-contract.json').read_text())['replacementDomainTexturePixels']
poly=[[(p[i]*source['scale']+source['translation'][i])*projection['scale']+projection['position'][i]for i in range(2)]for p in cut]
mask=Image.new('L',(left.shape[1],left.shape[0]));ImageDraw.Draw(mask).polygon([tuple(p)for p in poly],fill=255);outside=np.array(mask.filter(ImageFilter.MaxFilter(5)))==0
alpha=np.abs(left[:,:,3].astype(int)-right[:,:,3].astype(int));rgb=np.abs(left[:,:,:3].astype(int)-right[:,:,:3].astype(int));ys,xs=np.where((alpha>0)&outside)
result={'scope':'Native Godot transparent viewport, duplicates actual compiled draw-node projection with its transforms/order; not gameplay, alternative geometry, or performance scene. PNG magic and RGBA validated.', 'inputs':[li,ri], 'outsideAlphaChangedPixels':len(xs),'outsideMaxAlphaDelta':int(alpha[outside].max()),'outsideBecameZeroAlpha':int(((left[:,:,3]>0)&(right[:,:,3]==0)&outside).sum()),'outsideNewNonzeroAlpha':int(((left[:,:,3]==0)&(right[:,:,3]>0)&outside).sum()),'outsideOpaqueToBelowHalf':int(((left[:,:,3]>=245)&(right[:,:,3]<128)&outside).sum()),'outsideMaxRgbDelta':int(rgb[outside].max()),'alphaWitnesses':[{'pixel':[int(x),int(y)],'before':int(left[y,x,3]),'after':int(right[y,x,3])}for x,y in zip(xs,ys)],'rasterHaloClientPixels':2}
assert result['outsideBecameZeroAlpha']==0 and result['outsideNewNonzeroAlpha']==0 and result['outsideOpaqueToBelowHalf']==0
(root/'gpu-alpha-domain-result.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
