import json,hashlib,shutil
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[4]; OUT=Path(__file__).parent
base=ROOT/'packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/canonical-terrain'
data=[('uker-memory-overlay','call_9e1f38a873724480a822cde2cf9eef62.png',"Use case: precise-object-edit. Edit target: attached Uker memory-overlay terrain source. Change ONLY mottled surface paint to broad uniform flat matte color planes. Preserve exact existing composition, all stone block and plate outlines, thick dark joints, all purple overlay rings and squares, central circular node, diagonal bottom-left to top-right ghost architecture layout, shapes, positions, sizes and muted taupe/purple palette. Preserve semantic geometry. Remove irregular painted tonal blotches, surface grain, scratches and tiny surface cracks within planes, replacing each face with a single consistent matte tone; retain structural seams and contour bevels. Purple overlay areas should be clean broad planes rather than noisy patches. No new objects, no redesigned geometry, no extra ornament, no lighting changes, no gradient, no texture, no global blur or filter. Keep the full original square framing with complete edge-to-edge opaque coverage, no border, no crop, no padding. Output 1254 by 1254 pixels."),('uker-reality-rebuild','call_97d27e88ed4d42338eb466f9897230aa.png',"Use case: precise-object-edit. Edit target: attached Uker reality-rebuild terrain source. Change ONLY mottled surface paint to broad uniform flat matte color planes. Preserve exact existing composition and ALL rock block and plate outlines, thick dark joints, left rusty-red/orange side and right muted purple side, jagged central violet fissure with its original full vertical path, and central circular segmented node with all ring segments, positions, sizes and palette. Preserve semantic geometry including small stones and contour bevels. Remove irregular painted tonal blotches, surface grain, scratches and tiny surface cracks within planes, replacing each face with a single consistent matte tone. Fissure and stone faces should be clean broad planes rather than noisy patches. No new objects, no redesigned geometry, no extra ornament, no lighting changes, no gradients, no texture, no global blur or filter. Keep full original square framing with complete edge-to-edge opaque coverage, no border, no crop, no padding. Output 1254 by 1254 pixels.")]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
requests=[]; originals={}
for slug,filename,prompt in data:
 d=base/slug/'candidates/broad-matte-v2'; d.mkdir(parents=True,exist_ok=True)
 original=base/slug/'master.png'; source=ROOT.parent/'generated_images'/filename
 for n in ['raw.png','master.png']:shutil.copyfile(source,d/n)
 request={'prompt':prompt,'referenced_image_paths':[str(original)],'transparent_background':False}
 (d/'prompt.txt').write_text(prompt+'\n'); (d/'request.json').write_text(json.dumps(request,indent=2))
 im=Image.open(source); assert im.size==(1254,1254); assert im.convert('RGBA').getchannel('A').getextrema()==(255,255)
 originals[slug]={n:sha(base/slug/n) for n in ['raw.png','master.png']}
 (d/'provenance.json').write_text(json.dumps({'tool':'built-in image_gen.imagegen','attempt':1,'sourceEditTarget':str(original.relative_to(ROOT)),'sourceEditTargetSha256':sha(original),'returnedPath':str(source),'rawEqualsMaster':True,'sha256':sha(source),'size':list(im.size),'opaque':True,'processing':'Byte-exact copies only; no source recoding, resizing, filtering or cleanup.','status':'candidate-not-registered'},indent=2))
 requests.append({'slug':slug,**request})
(OUT/'reservation.json').write_text(json.dumps({'requests':requests},indent=2)); (OUT/'original-hashes.json').write_text(json.dumps(originals,indent=2))
old=ROOT/'docs/art-rebuild-20261019/approved-v5/scifi-terrain-batch-07/build_review.py'
s=old.read_text().replace("d=pack/'masters/approved-v5/canonical-terrain'/slug","d=pack/'masters/approved-v5/canonical-terrain'/slug/'candidates/broad-matte-v2'")
# Keep original generation provenance intact, write render provenance separately.
s=s.replace("(d/'provenance.json').write_text", "(d/'render-provenance.json').write_text")
(OUT/'build_review.py').write_text(s)
