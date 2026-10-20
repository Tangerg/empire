import hashlib,json,sys,shutil
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[4]; OUT=Path(__file__).parent
sys.path.insert(0,str(ROOT/'tools/art'))
from export_canonical_terrain_overrides import read_material,render_material,SCIFI_PACK,pixel_hash
pack=ROOT/SCIFI_PACK
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
records=json.loads((OUT/'reservation.json').read_text())['requests']
assets={}; reviews=[]; panels=[]
for req in records:
 slug=req['slug']; topic='c02-terrain-'+slug; d=pack/'masters/approved-v5/canonical-terrain'/slug/'candidates/broad-matte-v2'
 raw=d/'raw.png'; master=d/'master.png'; assert raw.read_bytes()==master.read_bytes()
 im=Image.open(master); w,h=im.size; assert w==h and w%2==0
 mode='variants' if slug == 'soler-blue-ice' else 'nesw-16'
 windows=[[0,0,w//2,h//2],[w//2,0,w,h//2],[0,h//2,w//2,h],[w//2,h//2,w,h]] if mode=='variants' else [[0,0,w,h]]
 entry={'source':str(master.relative_to(ROOT)),'sha256':sha(master),'registration':{'sourceSize':[w,h],'windowsXYXY':windows}}
 assets[topic]=entry
 source,prov=read_material(ROOT,entry,mode,SCIFI_PACK)
 sheet,samples,edges,cells=render_material(source,windows,mode,32)
 old=pack/'runtime/terrain'/('terrain-'+slug+'.png'); original=Image.open(old).convert('RGBA')
 (OUT/'original-reference').mkdir(exist_ok=True); shutil.copyfile(old,OUT/'original-reference'/old.name)
 assert sheet.getchannel('A').tobytes()==original.getchannel('A').tobytes()
 sheet.save(OUT/(slug+'-native32.png')); sheet.resize((sheet.width*6,192),Image.Resampling.NEAREST).save(OUT/(slug+'-zoom6.png'))
 panel=Image.new('RGB',(560,160),'#777777'); draw=ImageDraw.Draw(panel); draw.text((4,4),slug,fill='white')
 for y,bg in [(24,'black'),(68,'white'),(112,'#626843')]:
  panel.paste(bg,(0,y,560,y+40)); panel.paste(sheet,(4,y+4),sheet)
 panels.append(panel)
 # Connected-grid diagnostic uses actual cardinal adjacency, not arbitrarily tiled mask indices.
 grid=Image.new('RGBA',(256,256)); occupancy={(x,y) for y in range(8) for x in range(8) if (x+y)%5!=0}
 for x,y in occupancy:
  bits=sum(b for dx,dy,b in [(0,-1,1),(1,0,2),(0,1,4),(-1,0,8)] if (x+dx,y+dy) in occupancy)
  idx=(x+2*y)%4 if mode=='variants' else bits
  grid.paste(sheet.crop((idx*32,0,(idx+1)*32,32)),(x*32,y*32))
 display=Image.new('RGB',(768,256))
 for i,bg in enumerate(['black','white','#626843']):
  display.paste(bg,(i*256,0,(i+1)*256,256)); display.paste(grid,(i*256,0),grid)
 display.save(OUT/(slug+'-connected-grid-native32.png'))
 display.resize((1536,512),Image.Resampling.NEAREST).save(OUT/(slug+'-connected-grid-zoom2.png'))
 reviews.append({'topicId':topic,'tileMode':mode,'size':list(sheet.size),'source':prov,'samples':samples,'edges':edges,'cells':cells,'alphaMatchesOriginal':True,'originalSha256':sha(old),'pixelSha256':pixel_hash(sheet),'alphaSha256':pixel_hash(sheet.getchannel('A')),'distinctCells':len({c['pixelSha256'] for c in cells})})
(OUT/'proposed-registration.json').write_text(json.dumps({'schemaVersion':1,'assets':assets},indent=2)); (OUT/'native-review.json').write_text(json.dumps(reviews,indent=2))
all=Image.new('RGB',(560,len(panels)*160))
for i,p in enumerate(panels): all.paste(p,(0,i*160))
all.save(OUT/'all-native32.png'); all.resize((1120,all.height*2),Image.Resampling.NEAREST).save(OUT/'all-zoom2.png')
print(json.dumps([{'topic':r['topicId'],'distinctCells':r['distinctCells'],'edges':r['edges']} for r in reviews],indent=2))
