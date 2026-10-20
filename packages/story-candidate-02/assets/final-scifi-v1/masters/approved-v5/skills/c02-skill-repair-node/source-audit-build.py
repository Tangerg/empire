from pathlib import Path
from PIL import Image, ImageFilter
import ast, colorsys, hashlib, json
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
O=Path(__file__).parent
B=R/'packages/story-candidate-02/assets/final-scifi-v1'
code=B/'tools/build_final_scifi_v1.py'
# Execute only the four pure image functions. No module initialization or source writes.
tree=ast.parse(code.read_text()); wanted={'grid_cell','clear_white_edge_gutter','alpha_bbox','fit_transparent'}
ns={'Image':Image,'RESAMPLE':Image.Resampling.LANCZOS}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in wanted],type_ignores=[]),str(code),'exec'),ns)
for slug in ['c02-skill-mark-evacuation','c02-skill-publish-cargo','c02-skill-repair-node','c02-skill-rider-rescue-chain']:
 d=O/slug; sr=json.loads((d/'source-reference.json').read_text()); c=sr['sourceCellOneBased']
 alphapath=B/'intermediate/alpha/skills'/Path(sr['originalMaster']).name
 a=Image.open(alphapath).convert('RGBA')
 a.putdata([(r,g,b,0) if r>225 and b>175 and g<105 and alpha>0 else (r,g,b,alpha) for r,g,b,alpha in a.getdata()])
 cell=ns['grid_cell'](a,4,4,c['column']-1,c['row']-1,5)
 out=ns['fit_transparent'](cell,(48,48),2,False); px=[]
 for r,g,b,alpha in out.getdata():
  h,s,v=colorsys.rgb_to_hsv(r/255,g/255,b/255)
  spill=0.82<=h<=0.94 and s>=0.58 and v>=0.32 and g<max(r,b)*0.52
  px.append((r,g,b,0) if alpha>0 and ((r>230 and b>190 and g<60) or spill) else (r,g,b,alpha))
 out.putdata(px); old=Image.open(d/'source-runtime.png').convert('RGBA')
 diff=sum(x!=y for x,y in zip(out.getdata(),old.getdata()))
 report={'topicId':slug,'originalRuntimePixelMatch':diff==0,'differingPixels':diff,'comparedRGBABytes':len(out.tobytes()),'existingAlphaBoard':str(alphapath.relative_to(R)),'existingAlphaBoardSha256':hashlib.sha256(alphapath.read_bytes()).hexdigest(),'builderSha256':hashlib.sha256(code.read_bytes()).hexdigest(),'method':'Replayed four pure legacy builder functions in memory on the existing original alpha board; reproduced existing chroma postfilters in memory. No builder main, chroma helper, save, or repository write was called. Generated artwork was not used or modified.','sourceMappingConfirmed':diff==0}
 (d/'legacy-runtime-reconstruction.json').write_text(json.dumps(report,indent=2)+'\n')
 im=Image.open(d/'master.png'); al=im.getchannel('A');mask=al.point(lambda p:255 if p>=8 else 0).filter(ImageFilter.MinFilter(17)); coords=[]
 for y in range(im.height):
  for x in range(im.width):
   v=al.getpixel((x,y))
   if mask.getpixel((x,y)) and v<240: coords.append({'xy':[x,y],'rgba':list(im.getpixel((x,y)))})
 (d/'alpha-core-exceptions.json').write_text(json.dumps({'topicId':slug,'diagnosticOnly':True,'criterion':'Alpha below 240 within 8-pixel erosion of alpha >= 8 mask','pixels':coords,'count':len(coords),'sourceEdited':False},indent=2)+'\n')
 print(slug,'legacy pixel differences',diff,'core exception count',len(coords))
