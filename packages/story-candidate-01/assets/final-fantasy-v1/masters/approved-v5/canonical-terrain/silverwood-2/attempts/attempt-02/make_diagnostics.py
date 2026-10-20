import sys
sys.dont_write_bytecode=True
from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib,numpy as np
repo=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');p=Path(__file__).parent;sys.path.insert(0,str(repo/'tools/art'))
from export_canonical_terrain_overrides import render_material,review_previews,canonical_masks
im=Image.open(p/'raw.png').convert('RGB');native=p/'native-review';native.mkdir(exist_ok=True);reports={}
for size in (32,64):
 sheet,samples,edges,cells=render_material(im,[[0,0,1254,1254]],'nesw-16',size);sheet.save(native/f'diagnostic-sheet-{size}.png');alpha=np.array(sheet.getchannel('A'));reports[str(size)]={'sheetSize':list(sheet.size),'samples':samples,'edges':edges,'cells':cells,'alphaSHA256':hashlib.sha256(sheet.getchannel('A').tobytes()).hexdigest(),'alphaCounts':{'transparent':int((alpha==0).sum()),'partial':int(((alpha>0)&(alpha<255)).sum()),'opaque':int((alpha==255).sum())}}
 for name,fixture in review_previews(sheet,'nesw-16').items():
  fixture.save(native/f'{name}-{size}.png');bg=Image.new('RGBA',fixture.size,(83,93,89,255));bg.alpha_composite(fixture.convert('RGBA'));bg.convert('RGB').resize((fixture.width*(6 if size==32 else 3),fixture.height*(6 if size==32 else 3)),Image.Resampling.NEAREST).save(native/f'{name}-{size}-enlarged.png')
 bg=Image.new('RGBA',(768,824),(31,36,35,255));draw=ImageDraw.Draw(bg)
 for bits in range(16):
  tile=sheet.crop((bits*size,0,(bits+1)*size,size));rgb=Image.new('RGBA',(size,size),(77,86,80,255));rgb.alpha_composite(tile);x=(bits%4)*192;y=(bits//4)*206;bg.alpha_composite(rgb.resize((192,192),Image.Resampling.NEAREST),(x,y+14));draw.text((x+4,y),f'mask{bits}, native{size}',fill='white')
 bg.convert('RGB').save(p/f'fixed-mask-{size}-proof.png')
 # Show source color and mask together at true native and enlarged scales; diagnostics only.
 comp=Image.new('RGB',(768,476),(31,36,35));dr=ImageDraw.Draw(comp);source=im.resize((size,size),Image.Resampling.LANCZOS);mask=canonical_masks(size)[0];isolated=sheet.crop((0,0,size,size));composite=Image.new('RGBA',(size,size),(77,86,80,255));composite.alpha_composite(isolated)
 for col,(name,img) in enumerate([('full source sample',source),('fixed isolated pad',composite.convert('RGB')),('historical alpha',mask.convert('RGB'))]):
  comp.paste(img.resize((224,224),Image.Resampling.NEAREST),(col*256+16,24));comp.paste(img,(col*256+16,274));dr.text((col*256+16,6),f'{name} ({size}px)',fill='white')
 comp.save(p/f'native-center-compare-{size}.png')
(p/'diagnostic-export.json').write_text(json.dumps({'topicId':'C01-TERRAIN-SILVERWOOD-2','mode':'nesw-16','windowsXYXY':[[0,0,1254,1254]],'diagnosticOnly':True,'reports':reports},indent=2)+'\n');print(json.dumps({s:{k:r[k] for k in ['alphaSHA256','alphaCounts','edges']} for s,r in reports.items()},indent=2))
