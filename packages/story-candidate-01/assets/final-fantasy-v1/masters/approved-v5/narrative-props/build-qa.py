from pathlib import Path
from PIL import Image, ImageDraw, ImageOps
import json,hashlib,sys,shutil
import numpy as np
B=Path("/workspace/shared/empire-art-rebuild/approved-v5/narrative-props")
BG={"black":(0,0,0,255),"white":(255,255,255,255),"olive":(100,104,66,255),"dark":(28,33,42,255)}
SOURCES={
"prop-left-glove":"/workspace/scratch/84c9fe948755/generated_images/exec-57777bc0-7d95-4bf0-b29a-b9ffc9d62bfa.png",
"prop-exile-hardtack":"/workspace/scratch/84c9fe948755/generated_images/exec-fc658fc7-4c27-4b93-bb5a-8c21971550fd.png",
"prop-ivra-broken-bridle":"/workspace/scratch/84c9fe948755/generated_images/exec-ed052317-f4c7-4734-818f-68f6cd3847cb.png",
"prop-crown-fragment":"/workspace/scratch/84c9fe948755/generated_images/exec-321ac11b-4d24-49a2-8639-d755c280d440.png"
}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def stats(im):
 a=np.array(im.getchannel("A"));edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
 return dict(size=list(im.size),mode=im.mode,alphaExtrema=[int(a.min()),int(a.max())],transparentPixels=int((a==0).sum()),partialPixels=int(((a>0)&(a<255)).sum()),opaquePixels=int((a==255).sum()),nearOpaquePixels=int((a>=250).sum()),edgeMaxAlpha=int(edge.max()),edgeNonzeroPixels=int((edge>0).sum()),alphaBoundingBox=im.getchannel("A").getbbox(),alpha8BoundingBox=Image.fromarray((a>=8).astype("uint8")*255).getbbox())
for slug in sys.argv[1:]:
 d=B/slug;q=d/"QA";q.mkdir(exist_ok=True)
 raw=d/"raw/attempt-01.png"
 im=Image.open(raw).convert("RGBA")
 thumb=Image.new("RGBA",(128,128));thumb.paste(ImageOps.contain(im,(124,124),Image.Resampling.LANCZOS),(2,2))
 thumb.save(q/"contain-128.png")
 sheet=Image.new("RGB",(560,160),(215,215,215));dr=ImageDraw.Draw(sheet)
 for i,(name,color) in enumerate(BG.items()):
  comp=Image.new("RGBA",(128,128),color);comp.alpha_composite(thumb)
  comp.convert("RGB").save(q/f"native128-{name}.png")
  sheet.paste(comp.convert("RGB"),(i*140+6,25));dr.text((i*140+6,6),name,fill=(0,0,0))
 sheet.save(q/"native128-review.png")
 for name in ["white","dark"]:
  comp=Image.new("RGBA",im.size,BG[name]);comp.alpha_composite(im);comp.convert("RGB").save(q/f"source-{name}.png")
 reference=Path("/workspace/shared/empire-art-rebuild/approved-v5/narrative-keyprop-audit/original-crops")/("c01-"+slug)/"legacy-inset.png"
 ref=Image.open(reference).convert("RGB")
 src=Image.open(q/"source-white.png")
 contact=Image.new("RGB",(1234,647),(230,230,230));dr=ImageDraw.Draw(contact);dr.text((5,6),"Original 617px identity reference",fill=(0,0,0));dr.text((622,6),"New master, scaled only for side-by-side QA",fill=(0,0,0));contact.paste(ref,(0,30));contact.paste(src.resize((617,617),Image.Resampling.LANCZOS),(617,30));contact.save(q/"reference-fidelity.png")
 result={"source":stats(im),"contain128":stats(thumb),"wholeCanvasResize":{"source":list(im.size),"target":[128,128],"fit":[124,124],"padding":[2,2],"crop":None},"composites":[str(q/f"native128-{n}.png") for n in BG],"sourceBytesUnmodified":True,"alphaCleanup":False,"chromaKey":False,"bboxFit":False,"generatedSource":SOURCES.get(slug)}
 (q/"alpha-qa.json").write_text(json.dumps(result,indent=2)+"\n")
 print(slug,json.dumps(result["source"]),json.dumps(result["contain128"]))

