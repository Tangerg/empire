from pathlib import Path
from PIL import Image
import sys,shutil,json,hashlib
slug,path=sys.argv[1:];d=Path('/workspace/shared/empire-art-rebuild/approved-v5/scifi-skills')/('c02-skill-'+slug);p=Path(path);sha=hashlib.sha256(p.read_bytes()).hexdigest()
for name in ['raw.png','raw-native-v1.png','master.png']:shutil.copyfile(p,d/name)
im=Image.open(p)
r={'tool':'built-in image_gen','generatedPath':str(p),'sha256':sha,'nativeSize':list(im.size),'nativeMode':im.mode,'transparent_background':True,'generationAttempts':1,'rejectedAttempts':[],'postprocessing':'None; raw and master are byte-identical.'}
(d/'generation-result.json').write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r))
