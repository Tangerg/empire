from pathlib import Path
from PIL import Image
import json,hashlib,shutil,sys
b=Path('/workspace/shared/empire-art-rebuild/approved-v5/animation/shaman');pose=sys.argv[1];src=Path(sys.argv[2]);d=b/pose/(sys.argv[3] if len(sys.argv)>3 else 'attempt-01')
for n in ['raw.png','master.png']:shutil.copyfile(src,d/n)
im=Image.open(src);a=im.getchannel('A');hist=a.histogram();edges=list(a.crop((0,0,im.width,1)).getdata())+list(a.crop((0,im.height-1,im.width,im.height)).getdata())+list(a.crop((0,0,1,im.height)).getdata())+list(a.crop((im.width-1,0,im.width,im.height)).getdata());q={'nativeMode':im.mode,'nativeSize':list(im.size),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'alphaExtrema':a.getextrema(),'perimeterAlphaMax':max(edges),'perimeterAbove1':sum(v>1 for v in edges),'transparentPixels':hist[0],'semiTransparentPixels':sum(hist[1:255]),'opaquePixels':hist[255],'bboxAlpha1':a.point(lambda v:255 if v>1 else 0).getbbox(),'bboxAlpha127':a.point(lambda v:255 if v>127 else 0).getbbox(),'nativePixelsPreserved':True,'visualAssessment':'Direct black and white composites require visual review; original unassociated RGB is not used to infer haze.'};(d/'alpha-qa.json').write_text(json.dumps(q,indent=2)+'\n')
for color,name in [((255,255,255,255),'white'),((0,0,0,255),'black')]:
 out=Image.new('RGBA',im.size,color);out.alpha_composite(im);out.convert('RGB').save(d/f'qa-{name}.png')
hint=f'Generated images are saved to /workspace/scratch/84c9fe948755/generated_images as {src} by default.\nIf you need to use a generated image at another path, copy it and leave the original in place unless the user explicitly asks you to delete it.\nThe generated image is already displayed to the user. There is no need to render it in the final response as a Markdown image or file link.'
(d/'tool-result.json').write_text(json.dumps({'status':'success','generationSource':str(src),'outputHint':hint},indent=2)+'\n')
for n in ['raw.png','master.png','prompt.txt','request.json','tool-request.json','tool-result.json','alpha-qa.json','qa-black.png','qa-white.png']:shutil.copyfile(d/n,b/pose/n)
print(pose,json.dumps(q))
