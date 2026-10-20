from PIL import Image,ImageDraw,ImageEnhance,ImageFont
from pathlib import Path
import json,hashlib
REPO=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
OUT=Path(__file__).parent
PACK=REPO/'packages/story-candidate-01/assets/final-fantasy-v1'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
pxsha=lambda im:hashlib.sha256(im.convert('RGBA').tobytes()).hexdigest()
boards=['terrain-atlas-border-river.png','terrain-atlas-capital-silverwood.png','terrain-atlas-forge-wasteland.png','terrain-atlas-graveyard-oathlight.png']
manifest=json.load(open(PACK/'manifest-final-fantasy-v1.json'))
topics=[x for x in manifest['assets'] if x['category']=='terrain']
hd={x['topicId']:x for x in json.load(open(PACK/'manifest-tactical-runtime-hd.json'))['assets']}
connected={1,3,4,5,6,7,8,9,13,17,21,23,29,30}
allslots=[]; candidates={}; boardsmeta=[]
for bi,name in enumerate(boards):
 path=PACK/'masters/terrain'/name
 image=Image.open(path).convert('RGB')
 boardsmeta.append({'path':str(path.relative_to(REPO)),'sha256':sha(path),'dimensions':list(image.size),'sourceMode':Image.open(path).mode,'grid':[4,2],'inset':3})
 for row in range(2):
  for col in range(4):
   index=bi*8+row*4+col
   box=(round(col*image.width/4)+3,round(row*image.height/2)+3,round((col+1)*image.width/4)-3,round((row+1)*image.height/2)-3)
   cell=image.crop(box); crops=[]; variants={1:[],2:[]}
   side=round(min(cell.size)*.62)
   for cx,cy in ((.32,.32),(.68,.32),(.32,.68),(.68,.68)):
    x0=max(0,min(cell.width-side,round(cell.width*cx-side/2)))
    y0=max(0,min(cell.height-side,round(cell.height*cy-side/2)))
    crops.append([box[0]+x0,box[1]+y0,box[0]+x0+side,box[1]+y0+side])
    for density in (1,2):
     tile=cell.crop((x0,y0,x0+side,y0+side)).resize((32*density,32*density),Image.Resampling.LANCZOS)
     tile=ImageEnhance.Color(tile).enhance(.82)
     tile=ImageEnhance.Contrast(tile).enhance(.94)
     variants[density].append(tile.convert('RGBA'))
   meta={'sourceIndex':index,'board':str(path.relative_to(REPO)),'boardSHA256':sha(path),'slotRow':row,'slotColumn':col,'slotNumber1Based':row*4+col+1,'cropXYXY':list(box),'cropSize':list(cell.size),'variantCropXYXY':crops,'tileMode':'nesw-16' if index in connected else 'variants'}
   allslots.append(meta)
   for density in (1,2):
    size=32*density
    frames=variants[density]
    if index in connected:
     frames=[]
     for bits in range(16):
      mask=Image.new('L',(size,size),0); draw=ImageDraw.Draw(mask)
      if density==1:
       draw.rounded_rectangle((10,10,21,21),radius=4,fill=255)
       for bit,bounds in [(1,(12,0,19,16)),(2,(16,12,31,19)),(4,(12,16,19,31)),(8,(0,12,16,19))]:
        if bits&bit:draw.rectangle(bounds,fill=255)
      else:
       draw.rounded_rectangle((20,20,43,43),radius=8,fill=255)
       for bit,bounds in [(1,(24,0,39,32)),(2,(32,24,63,39)),(4,(24,32,39,63)),(8,(0,24,32,39))]:
        if bits&bit:draw.rectangle(bounds,fill=255)
      tile=variants[density][0].copy();tile.putalpha(mask);frames.append(tile)
    sheet=Image.new('RGBA',(size*len(frames),size))
    for fi,tile in enumerate(frames):sheet.alpha_composite(tile,(fi*size,0))
    # base write_asset's chroma cleanup is a no-op on this source family but include it precisely.
    if density==1:
     sheet.putdata([(r,g,b,0) if r>230 and b>190 and g<60 and a>0 else (r,g,b,a) for r,g,b,a in sheet.getdata()])
    candidates[(density,index)]=sheet
rows=[]
for topic in topics:
 rec={'topicId':topic['topicId'],'label':topic['label'],'manifest':topic,'runtime':{},'matches':{}}
 for density,path in [(1,PACK/topic['png']),(2,PACK/hd[topic['topicId']]['png'])]:
  image=Image.open(path).convert('RGBA'); pix=list(image.getchannel('A').getdata()); ahist=image.getchannel('A').histogram()
  matches=[idx for (den,idx),cand in candidates.items() if den==density and image.size==cand.size and image.tobytes()==cand.tobytes()]
  rec['runtime'][str(density)]={'path':str(path.relative_to(REPO)),'sha256':sha(path),'pixelSHA256':pxsha(image),'dimensions':list(image.size),'alphaCounts':{'transparent':ahist[0],'partial':sum(ahist[1:255]),'opaque':ahist[255]},'alphaBounds':list(image.getchannel('A').getbbox()),'mode':Image.open(path).mode}
  rec['matches'][str(density)]=matches
 if len(rec['matches']['1'])==1 and rec['matches']['1']==rec['matches']['2']:
  rec['mapping']='exact-RGBA-pixel-match-at-both-densities';rec['source']=allslots[rec['matches']['1'][0]]
 else:rec['mapping']='unresolved'
 rows.append(rec)
print('EXACT MATCHES',sum(r['mapping'].startswith('exact') for r in rows),'/',len(rows))
for r in rows:print(r['topicId'],r['label'],r['matches'],r.get('source',{}).get('slotNumber1Based'))
json.dump({'boards':boardsmeta,'topics':rows,'sourceSlots':allslots},open(OUT/'mapping-raw.json','w'),indent=2,ensure_ascii=False)
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
# proof thumbnails only, original boards remain in repository
for bi,name in enumerate(boards):
 im=Image.open(PACK/'masters/terrain'/name).convert('RGB')
 contact=Image.new('RGB',(1120,640),'#f3f0e8');d=ImageDraw.Draw(contact)
 for row in range(2):
  for col in range(4):
   idx=bi*8+row*4+col;meta=allslots[idx];tile=im.crop(meta['cropXYXY']);tile.thumbnail((260,252))
   x=col*280+10;y=row*320+35;contact.paste(tile,(x,y))
   match=next((r for r in rows if r.get('source',{}).get('sourceIndex')==idx),None)
   title=f"slot {row*4+col+1} [r{row},c{col}] index {idx}"
   d.text((x,row*320+8),title,fill='black',font=font)
   d.text((x,row*320+286),match['topicId'].replace('C01-TERRAIN-','') if match else 'UNMAPPED',fill='black',font=font)
   d.text((x,row*320+304),meta['tileMode'],fill='black',font=font)
 contact.save(OUT/(name.replace('.png','-proof.jpg')),quality=90)
