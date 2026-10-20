from pathlib import Path
import sys,json,hashlib,shutil
R=Path(__file__).parent
phase=sys.argv[1];p=R/phase;p.mkdir(parents=True,exist_ok=True)
prompt=sys.stdin.read();refs=sys.argv[2:]
(p/'prompt.txt').write_text(prompt)
(p/'request.json').write_text(json.dumps({'prompt':prompt,'referenced_image_paths':refs,'transparent_background':True},indent=2)+'\n')
s=p/'reference-snapshots';s.mkdir(exist_ok=True)
a=[]
for i,ref in enumerate(refs):
 dest=s/f'input-{i+1}.png';shutil.copy2(ref,dest);a.append({'path':ref,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'snapshot':str(dest.relative_to(R))})
(p/'reference-hashes.json').write_text(json.dumps(a,indent=2)+'\n')
